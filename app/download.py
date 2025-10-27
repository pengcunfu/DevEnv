import os
import asyncio
import aiohttp
import aiofiles
import threading
import time
import urllib.request
import platform
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Optional, Callable, Dict, Any, Union
import httpx
from tqdm import tqdm

class Downloader:
    """高级多线程下载器，支持断点续传、进度监控、速度限制等功能"""
    
    def __init__(self, url: str, save_path: str, 
                 max_workers: int = 8, 
                 chunk_size: int = 1024 * 1024,  # 1MB
                 timeout: int = 30,
                 max_retries: int = 3,
                 speed_limit: Optional[int] = None,  # bytes per second
                 progress_callback: Optional[Callable] = None,
                 proxy: Optional[Union[str, Dict[str, str]]] = None,
                 use_system_proxy: bool = True):
        """
        初始化下载器
        
        Args:
            url: 下载链接
            save_path: 保存路径
            max_workers: 最大并发数
            chunk_size: 块大小
            timeout: 超时时间
            max_retries: 最大重试次数
            speed_limit: 速度限制 (bytes/s)
            progress_callback: 进度回调函数
            proxy: 代理设置 (字符串或字典格式)
            use_system_proxy: 是否使用系统代理
        """
        self.url = url
        self.save_path = Path(save_path)
        self.max_workers = max_workers
        self.chunk_size = chunk_size
        self.timeout = timeout
        self.max_retries = max_retries
        self.speed_limit = speed_limit
        self.progress_callback = progress_callback
        self.proxy = proxy
        self.use_system_proxy = use_system_proxy
        
        # 状态控制
        self._is_running = False
        self._is_paused = False
        self._is_cancelled = False
        self._download_complete = False
        
        # 下载信息
        self.total_size = 0
        self.downloaded_size = 0
        self.download_speed = 0
        self.start_time = 0
        self.ranges = []
        self.support_range = True
        
        # 线程安全
        self._lock = threading.Lock()
        self._pause_event = threading.Event()
        self._stop_event = threading.Event()
        
        # 进度条
        self.progress_bar = None
        
        # 代理配置
        self.proxy_config = self._setup_proxy()
        
        # 初始化
        self._init_download()

    def _setup_proxy(self) -> Optional[Dict[str, str]]:
        """设置代理配置"""
        proxy_config = None
        
        # 如果指定了代理
        if self.proxy:
            if isinstance(self.proxy, str):
                # 字符串格式代理
                proxy_config = {
                    'http': self.proxy,
                    'https': self.proxy
                }
            elif isinstance(self.proxy, dict):
                # 字典格式代理
                proxy_config = self.proxy
        
        # 如果启用系统代理且没有手动指定代理
        elif self.use_system_proxy:
            proxy_config = self._get_system_proxy()
        
        if proxy_config:
            print(f"使用代理: {proxy_config}")
        
        return proxy_config
    
    def _get_system_proxy(self) -> Optional[Dict[str, str]]:
        """获取系统代理设置"""
        try:
            # 获取系统代理
            proxy_handler = urllib.request.getproxies()
            
            if proxy_handler:
                print(f"检测到系统代理: {proxy_handler}")
                return proxy_handler
            
            # Windows系统特殊处理
            if platform.system() == 'Windows':
                try:
                    import winreg
                    # 读取注册表中的代理设置
                    with winreg.OpenKey(winreg.HKEY_CURRENT_USER,
                                      r'Software\Microsoft\Windows\CurrentVersion\Internet Settings') as key:
                        proxy_enable = winreg.QueryValueEx(key, 'ProxyEnable')[0]
                        if proxy_enable:
                            proxy_server = winreg.QueryValueEx(key, 'ProxyServer')[0]
                            if proxy_server:
                                # 处理代理服务器格式
                                if '=' in proxy_server:
                                    # 协议特定代理
                                    proxies = {}
                                    for item in proxy_server.split(';'):
                                        if '=' in item:
                                            protocol, server = item.split('=', 1)
                                            proxies[protocol] = f'http://{server}'
                                    return proxies
                                else:
                                    # 通用代理
                                    return {
                                        'http': f'http://{proxy_server}',
                                        'https': f'http://{proxy_server}'
                                    }
                except Exception as e:
                    print(f"读取Windows代理设置失败: {e}")
            
        except Exception as e:
            print(f"获取系统代理失败: {e}")
        
        return None

    def _init_download(self):
        """初始化下载信息"""
        try:
            # 获取文件信息
            client_kwargs = {'timeout': self.timeout}
            if self.proxy_config:
                client_kwargs['proxies'] = self.proxy_config
                
            with httpx.Client(**client_kwargs) as client:
                response = client.head(self.url, follow_redirects=True)
                response.raise_for_status()
                
                self.total_size = int(response.headers.get('content-length', 0))
                accept_ranges = response.headers.get('accept-ranges', '').lower()
                
                if 'bytes' not in accept_ranges or self.total_size == 0:
                    self.support_range = False
                    self.max_workers = 1
                    
        except Exception as e:
            print(f"获取文件信息失败: {e}")
            self.support_range = False
            self.max_workers = 1
            
        # 计算下载范围
        self._calculate_ranges()
        
        # 检查已下载部分
        self._check_existing_file()

    def _calculate_ranges(self):
        """计算下载范围"""
        if not self.support_range or self.total_size == 0:
            self.ranges = [(0, self.total_size - 1 if self.total_size > 0 else 0)]
            return
            
        chunk_size = self.total_size // self.max_workers
        self.ranges = []
        
        for i in range(self.max_workers):
            start = i * chunk_size
            if i == self.max_workers - 1:
                end = self.total_size - 1
            else:
                end = start + chunk_size - 1
            self.ranges.append((start, end))

    def _check_existing_file(self):
        """检查已存在的文件"""
        if self.save_path.exists():
            existing_size = self.save_path.stat().st_size
            if existing_size == self.total_size:
                self.downloaded_size = self.total_size
                self._download_complete = True
            elif existing_size < self.total_size and self.support_range:
                self.downloaded_size = existing_size
            else:
                # 文件大小不匹配，重新下载
                self.save_path.unlink()
                self.downloaded_size = 0

    async def _download_chunk(self, session: aiohttp.ClientSession, 
                            start: int, end: int, 
                            chunk_id: int) -> bool:
        """下载单个块"""
        headers = {}
        if self.support_range:
            headers['Range'] = f'bytes={start}-{end}'
            
        retries = 0
        while retries < self.max_retries:
            try:
                async with session.get(self.url, headers=headers) as response:
                    response.raise_for_status()
                    
                    # 打开文件进行写入
                    async with aiofiles.open(self.save_path, 'r+b') as f:
                        await f.seek(start)
                        
                        async for chunk in response.content.iter_chunked(self.chunk_size):
                            # 检查暂停和停止状态
                            if self._is_cancelled:
                                return False
                                
                            while self._is_paused and not self._is_cancelled:
                                await asyncio.sleep(0.1)
                                
                            if self._is_cancelled:
                                return False
                                
                            # 写入数据
                            await f.write(chunk)
                            
                            # 更新进度
                            with self._lock:
                                self.downloaded_size += len(chunk)
                                
                            # 速度限制
                            if self.speed_limit:
                                await asyncio.sleep(len(chunk) / self.speed_limit)
                                
                            # 更新进度回调
                            if self.progress_callback:
                                self.progress_callback(self.get_progress())
                                
                return True
                
            except Exception as e:
                retries += 1
                print(f"块 {chunk_id} 下载失败 (重试 {retries}/{self.max_retries}): {e}")
                if retries < self.max_retries:
                    await asyncio.sleep(1)
                    
        return False

    async def _async_download(self):
        """异步下载主函数"""
        # 创建目录
        self.save_path.parent.mkdir(parents=True, exist_ok=True)
        
        # 创建或打开文件
        if not self.save_path.exists():
            async with aiofiles.open(self.save_path, 'wb') as f:
                if self.total_size > 0:
                    await f.truncate(self.total_size)
        
        # 创建进度条
        if not self.progress_callback:
            self.progress_bar = tqdm(
                total=self.total_size,
                initial=self.downloaded_size,
                unit='B',
                unit_scale=True,
                desc=f"下载 {self.save_path.name}"
            )
        
        # 创建HTTP会话
        connector = aiohttp.TCPConnector(limit=self.max_workers)
        timeout = aiohttp.ClientTimeout(total=self.timeout)
        
        # 设置会话参数
        session_kwargs = {
            'connector': connector,
            'timeout': timeout,
            'headers': {'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'}
        }
        
        # 添加代理配置
        if self.proxy_config:
            # aiohttp使用不同的代理格式
            if 'http' in self.proxy_config:
                session_kwargs['trust_env'] = True
                # 设置环境变量代理
                os.environ['HTTP_PROXY'] = self.proxy_config['http']
                if 'https' in self.proxy_config:
                    os.environ['HTTPS_PROXY'] = self.proxy_config['https']
        
        async with aiohttp.ClientSession(**session_kwargs) as session:
            
            # 创建下载任务
            tasks = []
            for i, (start, end) in enumerate(self.ranges):
                # 跳过已下载的部分
                if start < self.downloaded_size:
                    start = max(start, self.downloaded_size)
                    
                if start <= end:
                    task = asyncio.create_task(
                        self._download_chunk(session, start, end, i)
                    )
                    tasks.append(task)
            
            # 等待所有任务完成
            if tasks:
                results = await asyncio.gather(*tasks, return_exceptions=True)
                success = all(r is True for r in results if not isinstance(r, Exception))
                
                if not success:
                    raise Exception("部分下载任务失败")
        
        # 关闭进度条
        if self.progress_bar:
            self.progress_bar.close()
            
        self._download_complete = True

    def start(self):
        """开始下载"""
        if self._is_running:
            return
            
        if self._download_complete:
            print("文件已下载完成")
            return
            
        self._is_running = True
        self._is_cancelled = False
        self._is_paused = False
        self.start_time = time.time()
        
        # 在新线程中运行异步下载
        def run_async():
            try:
                loop = asyncio.new_event_loop()
                asyncio.set_event_loop(loop)
                loop.run_until_complete(self._async_download())
            except Exception as e:
                print(f"下载失败: {e}")
            finally:
                self._is_running = False
                
        thread = threading.Thread(target=run_async, daemon=True)
        thread.start()

    def pause(self):
        """暂停下载"""
        self._is_paused = True

    def resume(self):
        """恢复下载"""
        self._is_paused = False

    def cancel(self):
        """取消下载"""
        self._is_cancelled = True
        self._is_running = False
        if self.progress_bar:
            self.progress_bar.close()

    def get_progress(self) -> int:
        """获取下载进度百分比"""
        if self.total_size == 0:
            return 0
        return min(100, int(self.downloaded_size * 100 / self.total_size))

    def get_speed(self) -> float:
        """获取下载速度 (bytes/s)"""
        if not self.start_time or not self._is_running:
            return 0
        elapsed = time.time() - self.start_time
        if elapsed == 0:
            return 0
        return self.downloaded_size / elapsed

    def get_eta(self) -> int:
        """获取预计剩余时间 (秒)"""
        speed = self.get_speed()
        if speed == 0:
            return 0
        remaining = self.total_size - self.downloaded_size
        return int(remaining / speed)

    def get_status(self) -> Dict[str, Any]:
        """获取下载状态"""
        return {
            'progress': self.get_progress(),
            'downloaded': self.downloaded_size,
            'total': self.total_size,
            'speed': self.get_speed(),
            'eta': self.get_eta(),
            'is_running': self._is_running,
            'is_paused': self._is_paused,
            'is_cancelled': self._is_cancelled,
            'is_complete': self._download_complete
        }

    def set_proxy(self, proxy: Union[str, Dict[str, str], None]):
        """设置代理"""
        self.proxy = proxy
        self.proxy_config = self._setup_proxy()
        
    def disable_proxy(self):
        """禁用代理"""
        self.proxy = None
        self.use_system_proxy = False
        self.proxy_config = None
        
    def get_proxy_info(self) -> Optional[Dict[str, str]]:
        """获取当前代理信息"""
        return self.proxy_config
        
    def test_proxy(self) -> bool:
        """测试代理连接"""
        try:
            client_kwargs = {'timeout': 10}
            if self.proxy_config:
                client_kwargs['proxies'] = self.proxy_config
                
            with httpx.Client(**client_kwargs) as client:
                # 测试连接到一个简单的URL
                response = client.get('http://httpbin.org/ip')
                response.raise_for_status()
                print(f"代理测试成功，IP信息: {response.json()}")
                return True
        except Exception as e:
            print(f"代理测试失败: {e}")
            return False
