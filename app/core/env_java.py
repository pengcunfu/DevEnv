"""
Java环境管理
- Java版本切换
"""
import typing
from PyQt5 import QtCore
from PyQt5.QtWidgets import QApplication, QWidget
import sys
from PyQt5.QtWidgets import QMainWindow


class CoreApp(object):
    def changeJavaVersion():
        pass


class MyApp(QMainWindow):
    def __init__(self) -> None:
        super(QMainWindow, MyApp).__init__(self)
        self.resize(500, 200)
        self.setWindowTitle("Java版本切换工具")


if __name__ == "__main__":
    app = QApplication(sys.argv)
    main = MyApp()
    main.show()
    sys.exit(app.exec_())
