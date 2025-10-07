import os
import sys
import json
import shutil
import requests
import subprocess
from pathlib import Path
from PySide6.QtCore import QObject, Signal, QThread
from PySide6.QtWidgets import QMessageBox, QProgressDialog
from qfluentwidgets import MessageBox, ProgressBar

class UpdateChecker(QObject):
    """更新检查器"""
    update_available = Signal(dict)  # 发现新版本信号
    check_finished = Signal()  # 检查完成信号
    error = Signal(str)  # 错误信号

    def __init__(self, current_version, update_url):
        super().__init__()
        self.current_version = current_version
        self.update_url = update_url

    def check_update(self):
        """检查更新"""
        try:
            response = requests.get(self.update_url)
            response.raise_for_status()
            update_info = response.json()
            
            if self._compare_versions(update_info['version'], self.current_version) > 0:
                self.update_available.emit(update_info)
            self.check_finished.emit()
        except Exception as e:
            self.error.emit(f"检查更新失败: {str(e)}")

    def _compare_versions(self, version1, version2):
        """比较版本号"""
        v1_parts = [int(x) for x in version1.split('.')]
        v2_parts = [int(x) for x in version2.split('.')]
        
        for i in range(max(len(v1_parts), len(v2_parts))):
            v1 = v1_parts[i] if i < len(v1_parts) else 0
            v2 = v2_parts[i] if i < len(v2_parts) else 0
            
            if v1 > v2:
                return 1
            elif v1 < v2:
                return -1
        return 0

class UpdateDownloader(QThread):
    """更新下载器"""
    progress = Signal(int)
    finished = Signal(str)
    error = Signal(str)

    def __init__(self, download_url, save_path):
        super().__init__()
        self.download_url = download_url
        self.save_path = save_path

    def run(self):
        try:
            response = requests.get(self.download_url, stream=True)
            response.raise_for_status()
            
            total_size = int(response.headers.get('content-length', 0))
            block_size = 1024
            downloaded = 0
            
            with open(self.save_path, 'wb') as f:
                for data in response.iter_content(block_size):
                    downloaded += len(data)
                    f.write(data)
                    if total_size:
                        progress = int(downloaded * 100 / total_size)
                        self.progress.emit(progress)
            
            self.finished.emit(self.save_path)
        except Exception as e:
            self.error.emit(f"下载更新失败: {str(e)}")

class Updater:
    """更新管理器"""
    def __init__(self, parent=None):
        self.parent = parent
        self.current_version = "1.0.0"  # 当前版本
        self.update_url = "https://api.example.com/updates"  # 更新检查API
        self.temp_dir = Path("temp")
        self.temp_dir.mkdir(exist_ok=True)

    def check_for_updates(self):
        """检查更新"""
        checker = UpdateChecker(self.current_version, self.update_url)
        checker.update_available.connect(self._on_update_available)
        checker.error.connect(self._on_error)
        checker.check_finished.connect(lambda: print("检查完成"))
        
        # 在新线程中检查更新
        thread = QThread()
        checker.moveToThread(thread)
        thread.started.connect(checker.check_update)
        thread.start()

    def _on_update_available(self, update_info):
        """发现新版本"""
        msg = MessageBox(
            "发现新版本",
            f"当前版本: {self.current_version}\n"
            f"新版本: {update_info['version']}\n\n"
            f"更新内容:\n{update_info['changelog']}\n\n"
            "是否立即更新？",
            self.parent
        )
        
        if msg.exec():
            self._start_update(update_info)

    def _start_update(self, update_info):
        """开始更新"""
        download_url = update_info['download_url']
        save_path = self.temp_dir / f"update_{update_info['version']}.exe"
        
        # 创建进度对话框
        progress_dialog = ProgressBar(self.parent)
        progress_dialog.setWindowTitle("下载更新")
        progress_dialog.setRange(0, 100)
        progress_dialog.show()
        
        # 创建下载器
        downloader = UpdateDownloader(download_url, str(save_path))
        downloader.progress.connect(progress_dialog.setValue)
        downloader.finished.connect(lambda path: self._on_download_finished(path, update_info))
        downloader.error.connect(self._on_error)
        downloader.start()

    def _on_download_finished(self, update_file, update_info):
        """下载完成"""
        try:
            # 验证下载文件
            if not self._verify_update_file(update_file, update_info['checksum']):
                raise Exception("更新文件验证失败")
            
            # 启动更新程序
            self._launch_updater(update_file)
            
            # 退出当前程序
            QApplication.quit()
        except Exception as e:
            self._on_error(str(e))

    def _verify_update_file(self, file_path, expected_checksum):
        """验证更新文件"""
        # TODO: 实现文件校验
        return True

    def _launch_updater(self, update_file):
        """启动更新程序"""
        try:
            subprocess.Popen([str(update_file), '--update'])
        except Exception as e:
            raise Exception(f"启动更新程序失败: {str(e)}")

    def _on_error(self, error_msg):
        """处理错误"""
        MessageBox("更新错误", error_msg, self.parent).exec()

    def cleanup(self):
        """清理临时文件"""
        try:
            shutil.rmtree(self.temp_dir)
        except:
            pass
