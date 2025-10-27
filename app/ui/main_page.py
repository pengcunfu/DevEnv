from PySide6.QtWidgets import QWidget, QVBoxLayout, QTabWidget, QTableWidget, QTableWidgetItem, QHeaderView, QAbstractItemView, QPushButton, QComboBox, QLineEdit, QHBoxLayout, QLabel, QMessageBox
from PySide6.QtGui import QIcon
from PySide6.QtCore import QTimer
from app.ui.search_page import SearchDialog
import os

CACHE_DIR = os.path.join('data', 'cache')

class MainPage(QWidget):
    def __init__(self, software_tabs, download_worker_factory, parent=None):
        super().__init__(parent)
        self.setObjectName("MainPage")
        self.software_tabs = software_tabs
        self.download_worker_factory = download_worker_factory
        self.workers = []
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
        # Tab
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
                        worker = self.download_worker_factory(url, save_path)
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
        dlg = SearchDialog(self.software_tabs, self.download_worker_factory, self)
        dlg.search(keyword)
        dlg.exec() 