from PySide6.QtWidgets import QWidget, QVBoxLayout, QLabel, QLineEdit, QPushButton, QFileDialog, QMessageBox, QFormLayout
import os

class ConfigPage(QWidget):
    def __init__(self, config, on_save_callback=None, parent=None):
        super().__init__(parent)
        self.setObjectName("ConfigPage")
        self.setWindowTitle("程序配置")
        self.setMinimumSize(500, 200)
        self.config = config
        self.on_save_callback = on_save_callback
        self.init_ui()

    def init_ui(self):
        layout = QVBoxLayout()
        form = QFormLayout()
        # 下载地址
        self.download_url_edit = QLineEdit(self.config.get('download_url', ''))
        form.addRow(QLabel("软件下载配置文件地址："), self.download_url_edit)
        # 缓存目录
        self.cache_dir_edit = QLineEdit(self.config.get('cache_dir', 'data/cache'))
        browse_btn = QPushButton("选择目录")
        browse_btn.clicked.connect(self.browse_cache_dir)
        cache_layout = QVBoxLayout()
        cache_layout.addWidget(self.cache_dir_edit)
        cache_layout.addWidget(browse_btn)
        form.addRow(QLabel("缓存目录："), cache_layout)
        layout.addLayout(form)
        # 保存按钮
        save_btn = QPushButton("保存配置")
        save_btn.clicked.connect(self.save_config)
        layout.addWidget(save_btn)
        self.setLayout(layout)

    def browse_cache_dir(self):
        dir_path = QFileDialog.getExistingDirectory(self, "选择缓存目录", self.cache_dir_edit.text())
        if dir_path:
            self.cache_dir_edit.setText(dir_path)

    def save_config(self):
        self.config['download_url'] = self.download_url_edit.text().strip()
        self.config['cache_dir'] = self.cache_dir_edit.text().strip()
        if self.on_save_callback:
            self.on_save_callback(self.config)
        QMessageBox.information(self, "提示", "配置已保存！") 