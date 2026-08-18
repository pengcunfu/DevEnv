"""
将 PNG 图片转换为 ICO 图标文件
"""
from PIL import Image

# 读取 PNG 文件
img = Image.open('icon.png')

# 创建多个尺寸的图标（16x16, 32x32, 48x48, 256x256）
icon_sizes = [(16, 16), (32, 32), (48, 48), (256, 256)]

# 保存为 ICO 文件
img.save('icon.ico', format='ICO', sizes=icon_sizes)
print("图标转换成功: icon.ico")
