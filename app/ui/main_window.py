from PySide6.QtWidgets import QMainWindow, QApplication, QVBoxLayout, QHBoxLayout, QWidget, QPushButton, QStackedWidget, QFrame, QMenuBar, QMenu
from PySide6.QtCore import Qt
from PySide6.QtGui import QIcon, QAction

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
        
        # 设置窗口图标
        self.setWindowIcon(QIcon('resource/icon.png'))
        
        # 初始化更新器
        self.updater = Updater(self)
        
        # 创建菜单栏
        self.create_menu_bar()
        
        # 创建中央窗口部件
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        
        # 创建主布局（垂直布局，只包含内容区域）
        main_layout = QVBoxLayout(central_widget)
        main_layout.setContentsMargins(5, 5, 5, 5)
        
        # 创建内容区域
        self.stacked_widget = QStackedWidget()
        main_layout.addWidget(self.stacked_widget)
        
        # 初始化界面
        self.init_interface()
        
        # 默认显示主页
        self.switch_page(0)
        
        # 暂时禁用自动更新检查，避免启动时的线程问题
        # self.check_update()
    
    def create_menu_bar(self):
        """创建菜单栏"""
        menubar = self.menuBar()
        
        # 视图菜单
        view_menu = menubar.addMenu('视图(&V)')
        
        # 主页动作
        self.home_action = QAction('主页(&H)', self)
        self.home_action.setShortcut('Ctrl+H')
        self.home_action.setStatusTip('显示主页')
        self.home_action.triggered.connect(lambda: self.switch_page(0))
        view_menu.addAction(self.home_action)
        
        # 搜索动作
        self.search_action = QAction('搜索(&S)', self)
        self.search_action.setShortcut('Ctrl+F')
        self.search_action.setStatusTip('打开搜索页面')
        self.search_action.triggered.connect(lambda: self.switch_page(1))
        view_menu.addAction(self.search_action)
        
        # 下载管理动作
        self.download_action = QAction('下载管理(&D)', self)
        self.download_action.setShortcut('Ctrl+D')
        self.download_action.setStatusTip('打开下载管理页面')
        self.download_action.triggered.connect(lambda: self.switch_page(2))
        view_menu.addAction(self.download_action)
        
        view_menu.addSeparator()
        
        # 设置动作
        self.config_action = QAction('设置(&C)', self)
        self.config_action.setShortcut('Ctrl+,')
        self.config_action.setStatusTip('打开设置页面')
        self.config_action.triggered.connect(lambda: self.switch_page(3))
        view_menu.addAction(self.config_action)
        
        # 工具菜单
        tools_menu = menubar.addMenu('工具(&T)')
        
        # 检查更新动作
        update_action = QAction('检查更新(&U)', self)
        update_action.setStatusTip('检查软件更新')
        update_action.triggered.connect(self.check_update)
        tools_menu.addAction(update_action)
        
        # 帮助菜单
        help_menu = menubar.addMenu('帮助(&H)')
        
        # 关于动作
        about_action = QAction('关于(&A)', self)
        about_action.setStatusTip('关于软件下载管理器')
        about_action.triggered.connect(self.show_about)
        help_menu.addAction(about_action)
        
        # 创建状态栏
        self.statusBar().showMessage('就绪')

    def init_interface(self):
        # 加载软件配置
        import yaml
        try:
            with open('data/link_config.yaml', 'r', encoding='utf-8') as f:
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
        # 切换页面
        self.stacked_widget.setCurrentIndex(index)
        
        # 更新状态栏
        page_names = ['主页', '搜索', '下载管理', '设置']
        if 0 <= index < len(page_names):
            self.statusBar().showMessage(f'当前页面: {page_names[index]}')
        
    def on_config_save(self, config):
        # 处理配置保存
        pass
        
    def check_update(self):
        """检查更新"""
        self.updater.check_for_updates()
        
    def show_about(self):
        """显示关于对话框"""
        from PySide6.QtWidgets import QMessageBox
        QMessageBox.about(self, "关于软件下载管理器", 
                         "软件下载管理器 v1.0\n\n"
                         "一个功能强大的软件下载管理工具\n"
                         "支持多线程下载、断点续传、代理设置等功能\n\n"
                         "© 2024 熔岩DEV")
        
    def closeEvent(self, event):
        """关闭窗口时清理临时文件"""
        self.updater.cleanup()
        super().closeEvent(event) 