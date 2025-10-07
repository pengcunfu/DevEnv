import os
import threading
import requests

class Downloader:
    def __init__(self, url, save_path, num_threads=4, chunk_size=1024*1024):
        self.url = url
        self.save_path = save_path
        self.num_threads = num_threads
        self.chunk_size = chunk_size
        self.threads = []
        self.stop_flag = threading.Event()
        self.pause_flag = threading.Event()
        self.progress = 0
        self.total = 0
        self.downloaded = 0
        self.ranges = []
        self.lock = threading.Lock()
        self.support_range = True
        self._init_file()

    def _init_file(self):
        # 获取文件总大小和Range支持
        headers = {'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'}
        r = requests.head(self.url, allow_redirects=True, headers=headers)
        self.total = int(r.headers.get('content-length', 0))
        accept_ranges = r.headers.get('accept-ranges', '').lower()
        if 'bytes' not in accept_ranges:
            self.support_range = False
            self.num_threads = 1
        # 计算分块
        self.ranges = []
        part = self.total // self.num_threads
        for i in range(self.num_threads):
            start = i * part
            end = (i + 1) * part - 1 if i < self.num_threads - 1 else self.total - 1
            self.ranges.append([start, end])
        # 创建文件并预分配空间
        if not os.path.exists(self.save_path):
            with open(self.save_path, 'wb') as f:
                f.truncate(self.total)
        # 计算已下载
        self.downloaded = self._calc_downloaded()

    def _calc_downloaded(self):
        downloaded = 0
        if os.path.exists(self.save_path):
            downloaded = os.path.getsize(self.save_path)
        return downloaded

    def _download_range(self, start, end):
        headers = {
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'
        }
        if self.support_range:
            headers['Range'] = f'bytes={start}-{end}'
        try:
            with requests.get(self.url, headers=headers, stream=True) as r:
                r.raise_for_status()
                with open(self.save_path, 'r+b') as f:
                    f.seek(start)
                    for chunk in r.iter_content(chunk_size=self.chunk_size):
                        if self.stop_flag.is_set():
                            return
                        while self.pause_flag.is_set():
                            threading.Event().wait(0.1)
                        if chunk:
                            f.write(chunk)
                            with self.lock:
                                self.downloaded += len(chunk)
        except Exception as e:
            print(f"下载线程异常: {e}")
            raise

    def start(self):
        self.stop_flag.clear()
        self.pause_flag.clear()
        self.threads = []
        for start, end in self.ranges:
            t = threading.Thread(target=self._download_range, args=(start, end))
            t.daemon = True
            t.start()
            self.threads.append(t)

    def pause(self):
        self.pause_flag.set()

    def resume(self):
        self.pause_flag.clear()

    def stop(self):
        self.stop_flag.set()
        for t in self.threads:
            t.join()

    def get_progress(self):
        if self.total == 0:
            return 0
        return int(self.downloaded * 100 / self.total) 