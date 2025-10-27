from PySide6.QtWidgets import QMainWindow, QApplication, QVBoxLayout, QHBoxLayout, QWidget, QPushButton, QStackedWidget, QFrame
from PySide6.QtCore import Qt
from PySide6.QtGui import QIcon

from .main_page import MainPage
from .search_page import SearchDialog
from .download_manager import DownloadManagerPage
from .config_page import ConfigPage
from app.update import Updater

class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.setWindowTitle('软件下载管理器')
        self.resize(900, 700)
        
        # 初始化更新器
        self.updater = Updater(self)
        
        # 创建中央窗口部件
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        
        # 创建主布局
        main_layout = QHBoxLayout(central_widget)
        
        # 创建导航栏
        self.nav_frame = QFrame()
        self.nav_frame.setFixedWidth(200)
        self.nav_frame.setFrameStyle(QFrame.StyledPanel)
        nav_layout = QVBoxLayout(self.nav_frame)
        
        # 创建导航按钮
        self.nav_buttons = []
        self.home_btn = QPushButton('主页')
        self.search_btn = QPushButton('搜索')
        self.download_btn = QPushButton('下载管理')
        self.config_btn = QPushButton('设置')
        
        self.nav_buttons = [self.home_btn, self.search_btn, self.download_btn, self.config_btn]
        
        for btn in self.nav_buttons:
            btn.setCheckable(True)
            btn.setMinimumHeight(40)
            nav_layout.addWidget(btn)
        
        nav_layout.addStretch()
        
        # 创建内容区域
        self.stacked_widget = QStackedWidget()
        
        # 添加到主布局
        main_layout.addWidget(self.nav_frame)
        main_layout.addWidget(self.stacked_widget)
        
        # 初始化界面
        self.init_interface()
        
        # 连接导航按钮信号
        self.home_btn.clicked.connect(lambda: self.switch_page(0))
        self.search_btn.clicked.connect(lambda: self.switch_page(1))
        self.download_btn.clicked.connect(lambda: self.switch_page(2))
        self.config_btn.clicked.connect(lambda: self.switch_page(3))
        
        # 默认选择主页
        self.switch_page(0)
        
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
        
        # 创建页面
        self.main_page = MainPage(software_tabs, download_worker_factory)
        self.search_page = SearchDialog(software_tabs, download_worker_factory, self)
        self.download_manager = DownloadManagerPage()
        self.config_page = ConfigPage({}, self.on_config_save)
        
        # 添加页面到堆叠窗口部件
        self.stacked_widget.addWidget(self.main_page)
        self.stacked_widget.addWidget(self.search_page)
        self.stacked_widget.addWidget(self.download_manager)
        self.stacked_widget.addWidget(self.config_page)
        
    def switch_page(self, index):
        """切换页面"""
        # 更新按钮状态
        for i, btn in enumerate(self.nav_buttons):
            btn.setChecked(i == index)
        
        # 切换页面
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