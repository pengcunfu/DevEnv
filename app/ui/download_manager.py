from PySide6.QtWidgets import QWidget, QVBoxLayout, QTableWidget, QTableWidgetItem, QHeaderView, QAbstractItemView, QProgressBar, QLabel
from PySide6.QtCore import Qt

class DownloadManagerPage(QWidget):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setObjectName("DownloadManagerPage")
        self.setWindowTitle("下载进度管理")
        self.setMinimumSize(700, 400)
        self.init_ui()
        self.downloads = []  # [(name, version, progress, status)]

    def init_ui(self):
        layout = QVBoxLayout()
        self.table = QTableWidget(0, 4)
        self.table.setHorizontalHeaderLabels(["名称", "版本", "进度", "状态"])
        self.table.horizontalHeader().setSectionResizeMode(QHeaderView.Stretch)
        self.table.setEditTriggers(QAbstractItemView.NoEditTriggers)
        layout.addWidget(QLabel("所有下载任务："))
        layout.addWidget(self.table)
        self.setLayout(layout)

    def add_download(self, name, version):
        row = self.table.rowCount()
        self.table.insertRow(row)
        self.table.setItem(row, 0, QTableWidgetItem(name))
        self.table.setItem(row, 1, QTableWidgetItem(version))
        progress_bar = QProgressBar()
        progress_bar.setValue(0)
        self.table.setCellWidget(row, 2, progress_bar)
        self.table.setItem(row, 3, QTableWidgetItem("等待中"))
        self.downloads.append((name, version, progress_bar))
        return progress_bar, row

    def update_progress(self, row, value, status=None):
        progress_bar = self.table.cellWidget(row, 2)
        if progress_bar:
            progress_bar.setValue(value)
        if status:
            self.table.setItem(row, 3, QTableWidgetItem(status)) 