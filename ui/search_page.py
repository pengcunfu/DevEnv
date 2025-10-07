from PySide6.QtWidgets import QDialog, QWidget, QVBoxLayout, QTableWidget, QTableWidgetItem, QHeaderView, QAbstractItemView, QComboBox, QPushButton, QMessageBox, QLineEdit, QHBoxLayout, QLabel
from PySide6.QtGui import QIcon
from PySide6.QtCore import QTimer
import os

CACHE_DIR = os.path.join('data', 'cache')

class SearchDialog(QDialog):
    def __init__(self, all_softwares, download_worker_factory, parent=None):
        super().__init__(parent)
        self.setObjectName("SearchDialog")
        self.setWindowTitle("软件搜索")
        self.setMinimumSize(700, 400)
        self.all_softwares = all_softwares
        self.download_worker_factory = download_worker_factory
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