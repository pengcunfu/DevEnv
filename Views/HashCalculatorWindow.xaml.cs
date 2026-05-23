using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevEnv.ViewModels;

namespace DevEnv.Views
{
    public partial class HashCalculatorWindow : Window
    {
        public HashCalculatorWindow()
        {
            InitializeComponent();
            DataContext = new HashCalculatorViewModel();

            // 设置默认选中的哈希算法
            HashTypeComboBox.SelectedIndex = 2; // SHA-256
        }

        private void Border_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                var border = sender as Border;
                if (border != null)
                {
                    border.Background = System.Windows.Media.Brushes.LightBlue;
                    border.Opacity = 0.8;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Border_DragLeave(object sender, DragEventArgs e)
        {
            var border = sender as Border;
            if (border != null)
            {
                border.Background = System.Windows.Media.Brushes.Transparent;
                border.Opacity = 1.0;
            }
        }

        private void Border_Drop(object sender, DragEventArgs e)
        {
            var border = sender as Border;
            if (border != null)
            {
                border.Background = System.Windows.Media.Brushes.Transparent;
                border.Opacity = 1.0;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    var viewModel = DataContext as HashCalculatorViewModel;
                    if (viewModel != null)
                    {
                        // 切换到文件哈希标签页
                        MainTabControl.SelectedItem = FileHashTab;

                        // 设置文件路径
                        viewModel.FilePath = files[0];

                        // 自动计算哈希
                        viewModel.CalculateHashCommand.Execute(null);
                    }
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Ctrl+V 粘贴文件路径
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var viewModel = DataContext as HashCalculatorViewModel;
                if (viewModel != null && MainTabControl.SelectedItem == FileHashTab)
                {
                    viewModel.PastePathCommand.Execute(null);
                }
            }
            // Ctrl+C 复制第一个哈希值
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var viewModel = DataContext as HashCalculatorViewModel;
                if (viewModel != null && viewModel.HashResults.Count > 0)
                {
                    viewModel.CopyHashCommand.Execute(viewModel.HashResults[0].Hash);
                }
            }
        }
    }
}