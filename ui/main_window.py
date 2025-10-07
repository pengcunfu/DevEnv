from PySide6.QtWidgets import QMainWindow, QApplication
from PySide6.QtCore import Qt
from qfluentwidgets import NavigationInterface, NavigationItemPosition, FluentIcon, SubtitleLabel, setTheme, Theme
from qfluentwidgets import FluentWindow, NavigationWidget, MessageBox

from .main_page import MainPage
from .search_page import SearchDialog
from .download_manager import DownloadManagerPage
from .config_page import ConfigPage
from app.update import Updater

class MainWindow(FluentWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle('软件下载管理器')
        self.resize(900, 700)
        
        # 初始化更新器
        self.updater = Updater(self)
        
        # 初始化界面
        self.init_interface()
        
        # 设置主题
        setTheme(Theme.AUTO)
        
        # 暂时禁用自动更新检查，避免启动时的线程问题
        # self.check_update()
        
    def init_interface(self):
        # 加载软件配置
        import yaml
        try:
            with open('link_config.yaml', 'r', encoding='utf-8') as f:
                software_tabs = yaml.safe_load(f)
        except:
            software_tabs = {}
        
        # 创建下载工作器工厂
        from main import DownloadWorker
        def download_worker_factory(url, save_path):
            return DownloadWorker(url, save_path)
        
        # 添加页面到导航界面
        self.main_page = MainPage(software_tabs, download_worker_factory)
        self.search_page = SearchDialog(software_tabs, download_worker_factory, self)
        self.download_manager = DownloadManagerPage()
        self.config_page = ConfigPage({}, self.on_config_save)
        
        self.addSubInterface(self.main_page, FluentIcon.HOME, '主页')
        self.addSubInterface(self.search_page, FluentIcon.SEARCH, '搜索')
        self.addSubInterface(self.download_manager, FluentIcon.DOWNLOAD, '下载管理')
        self.addSubInterface(self.config_page, FluentIcon.SETTING, '设置')
        
    def on_navigation_changed(self, index):
        self.stacked_widget.setCurrentIndex(index)
        
    def on_config_save(self, config):
        # 处理配置保存
        pass
        
    def check_update(self):
        """检查更新"""
        self.updater.check_for_updates()
        
    def closeEvent(self, event):
        """关闭窗口时清理临时文件"""
        self.updater.cleanup()
        super().closeEvent(event) 