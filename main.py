import sys
import os
import yaml
from app.download import Downloader
from PySide6.QtWidgets import (
    QApplication, QWidget, QVBoxLayout, QTabWidget, QTableWidget, QTableWidgetItem, QHeaderView, QAbstractItemView, QPushButton, QComboBox, QMessageBox, QLineEdit, QHBoxLayout, QDialog, QLabel
)
from PySide6.QtGui import QIcon
from PySide6.QtCore import QSize, QThread, Signal, QTimer
import traceback
from app.ui.main_window import MainWindow

CONFIG_PATH = 'data/link_config.yaml'
CACHE_DIR = os.path.join('data', 'cache')

if not os.path.exists(CACHE_DIR):
    os.makedirs(CACHE_DIR)

def load_software_config(path):
    with open(path, 'r', encoding='utf-8') as f:
        return yaml.safe_load(f)

class DownloadWorker(QThread):
    progress = Signal(int)
    finished = Signal(str)
    error = Signal(str)

    def __init__(self, url, save_path):
        super().__init__()
        self.url = url
        self.save_path = save_path
        self.downloader = Downloader(url, save_path)

    def run(self):
        try:
            self.downloader.start()
            while True:
                progress = self.downloader.get_progress()
                self.progress.emit(progress)
                if progress >= 100:
                    break
                self.msleep(200)
            self.finished.emit(self.save_path)
        except Exception as e:
            tb = traceback.format_exc()
            print(f"下载线程异常: {e}\n{tb}")
            self.error.emit(f"{e}\n{tb}")

class SearchDialog(QDialog):
    def __init__(self, all_softwares, parent=None):
        super().__init__(parent)
        self.setWindowTitle("软件搜索")
        self.setMinimumSize(700, 400)
        self.all_softwares = all_softwares
        self.workers = []
        self.init_ui()

    def init_ui(self):
        layout = QVBoxLayout()
        self.result_table = QTableWidget(0, 5)
        self.result_table.setHorizontalHeaderLabels(["程序名称", "图标", "描述", "版本", "下载"])
        self.result_table.horizontalHeader().setSectionResizeMode(QHeaderView.Stretch)
        self.result_table.setEditTriggers(QAbstractItemView.NoEditTriggers)
        layout.addWidget(self.result_table)
        self.setLayout(layout)

    def search(self, keyword):
        results = []
        for tab_name, sw_list in self.all_softwares.items():
            for sw in sw_list:
                name = sw.get('name', '')
                desc = sw.get('desc', '')
                icon_path = sw.get('icon', None)
                versions = sw.get('versions', [])
                if keyword.lower() in name.lower() or keyword.lower() in desc.lower():
                    results.append((name, icon_path, desc, versions))
        self.result_table.setRowCount(len(results))
        for row, (name, icon_path, desc, versions) in enumerate(results):
            self.result_table.setItem(row, 0, QTableWidgetItem(name))
            icon_item = QTableWidgetItem()
            if icon_path:
                icon_item.setIcon(QIcon(icon_path))
            self.result_table.setItem(row, 1, icon_item)
            self.result_table.setItem(row, 2, QTableWidgetItem(desc))
            combo = QComboBox()
            for ver in versions:
                combo.addItem(str(ver.get('version', '')), ver.get('url', ''))
            self.result_table.setCellWidget(row, 3, combo)
            btn = QPushButton("下载")
            self.result_table.setCellWidget(row, 4, btn)
            def make_download_func(cmb, name, btn):
                def download():
                    url = cmb.currentData()
                    if not url:
                        QMessageBox.warning(self, "错误", "未找到下载链接")
                        return
                    filename = f"{name}_{cmb.currentText()}.exe"
                    save_path = os.path.join(CACHE_DIR, filename)
                    btn.setEnabled(False)
                    anim_states = ["下载中", "下载中.", "下载中..", "下载中..."]
                    anim_idx = [0]
                    timer = QTimer()
                    def update_anim():
                        btn.setText(f"{anim_states[anim_idx[0]]} 0%")
                        anim_idx[0] = (anim_idx[0] + 1) % len(anim_states)
                    timer.timeout.connect(update_anim)
                    timer.start(400)
                    worker = DownloadWorker(url, save_path)
                    self.workers.append(worker)
                    def on_progress(val):
                        btn.setText(f"{anim_states[anim_idx[0]]} {val}%")
                    def on_finish(path):
                        timer.stop()
                        btn.setText("下载")
                        btn.setEnabled(True)
                        QMessageBox.information(self, "下载完成", f"已保存到: {path}")
                    def on_error(msg):
                        timer.stop()
                        btn.setText("下载")
                        btn.setEnabled(True)
                        QMessageBox.critical(self, "下载失败", msg)
                    worker.progress.connect(on_progress)
                    worker.finished.connect(on_finish)
                    worker.error.connect(on_error)
                    worker.start()
                return download
            btn.clicked.connect(make_download_func(combo, name, btn))

class SoftwareManager(QWidget):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("熔岩DEV软件管理")
        self.setMinimumSize(800, 500)
        self.software_tabs = load_software_config(CONFIG_PATH)
        self.workers = []  # 保持线程引用，防止被回收
        self.init_ui()

    def init_ui(self):
        main_layout = QVBoxLayout()
        # 搜索栏
        search_layout = QHBoxLayout()
        self.search_edit = QLineEdit()
        self.search_edit.setPlaceholderText("请输入软件名称或描述进行搜索...")
        search_btn = QPushButton("搜索")
        search_btn.clicked.connect(self.open_search_dialog)
        search_layout.addWidget(QLabel("🔍"))
        search_layout.addWidget(self.search_edit)
        search_layout.addWidget(search_btn)
        main_layout.addLayout(search_layout)
        # 原有Tab
        self.tabs = QTabWidget()
        for tab_name, sw_list in self.software_tabs.items():
            table = QTableWidget(len(sw_list), 5)
            table.setHorizontalHeaderLabels(["程序名称", "图标", "描述", "版本", "下载"])
            table.horizontalHeader().setSectionResizeMode(QHeaderView.Stretch)
            table.setEditTriggers(QAbstractItemView.NoEditTriggers)
            for row, sw in enumerate(sw_list):
                name = sw.get('name', '')
                desc = sw.get('desc', '')
                icon_path = sw.get('icon', None)
                versions = sw.get('versions', [])
                table.setItem(row, 0, QTableWidgetItem(name))
                icon_item = QTableWidgetItem()
                if icon_path:
                    icon_item.setIcon(QIcon(icon_path))
                table.setItem(row, 1, icon_item)
                table.setItem(row, 2, QTableWidgetItem(desc))
                combo = QComboBox()
                for ver in versions:
                    combo.addItem(str(ver.get('version', '')), ver.get('url', ''))
                table.setCellWidget(row, 3, combo)
                btn = QPushButton("下载")
                table.setCellWidget(row, 4, btn)
                def make_download_func(cmb, name, btn):
                    def download():
                        url = cmb.currentData()
                        if not url:
                            QMessageBox.warning(self, "错误", "未找到下载链接")
                            return
                        filename = f"{name}_{cmb.currentText()}.exe"
                        save_path = os.path.join(CACHE_DIR, filename)
                        btn.setEnabled(False)
                        anim_states = ["下载中", "下载中.", "下载中..", "下载中..."]
                        anim_idx = [0]
                        timer = QTimer()
                        def update_anim():
                            btn.setText(f"{anim_states[anim_idx[0]]} 0%")
                            anim_idx[0] = (anim_idx[0] + 1) % len(anim_states)
                        timer.timeout.connect(update_anim)
                        timer.start(400)
                        worker = DownloadWorker(url, save_path)
                        self.workers.append(worker)
                        def on_progress(val):
                            btn.setText(f"{anim_states[anim_idx[0]]} {val}%")
                        def on_finish(path):
                            timer.stop()
                            btn.setText("下载")
                            btn.setEnabled(True)
                            QMessageBox.information(self, "下载完成", f"已保存到: {path}")
                        def on_error(msg):
                            timer.stop()
                            btn.setText("下载")
                            btn.setEnabled(True)
                            QMessageBox.critical(self, "下载失败", msg)
                        worker.progress.connect(on_progress)
                        worker.finished.connect(on_finish)
                        worker.error.connect(on_error)
                        worker.start()
                    return download
                btn.clicked.connect(make_download_func(combo, name, btn))
            self.tabs.addTab(table, tab_name)
        main_layout.addWidget(self.tabs)
        self.setLayout(main_layout)

    def open_search_dialog(self):
        keyword = self.search_edit.text().strip()
        if not keyword:
            QMessageBox.information(self, "提示", "请输入搜索关键词！")
            return
        dlg = SearchDialog(self.software_tabs, self)
        dlg.search(keyword)
        dlg.exec()

def main():
    app = QApplication(sys.argv)
    
    # 设置应用程序图标
    app.setWindowIcon(QIcon('resource/icon.png'))
    
    window = MainWindow()
    window.show()
    sys.exit(app.exec())

if __name__ == '__main__':
    main()
