using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Media;
using DevEnv.UI;

namespace DevEnv.Views
{
    public partial class JsonFormatterWindow : Window
    {
        public JsonFormatterWindow()
        {
            InitializeComponent();
        }

        private void BtnFormat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? input = TxtInput.Text;
                if (string.IsNullOrWhiteSpace(input))
                {
                    UpdateStatus("请输入 JSON 内容", true);
                    return;
                }

                // 解析 JSON
                var jsonDoc = JsonDocument.Parse(input);

                // 格式化输出
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string formatted = JsonSerializer.Serialize(jsonDoc.RootElement, options);
                TxtOutput.Text = formatted;

                UpdateStatus($"格式化成功 - {formatted.Length} 字符", false);
            }
            catch (JsonException ex)
            {
                TxtOutput.Text = $"JSON 错误: {ex.Message}";
                UpdateStatus($"JSON 格式化失败: {ex.Message}", true);
            }
            catch (Exception ex)
            {
                TxtOutput.Text = $"错误: {ex.Message}";
                UpdateStatus($"操作失败: {ex.Message}", true);
            }
        }

        private void BtnMinify_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? input = TxtInput.Text;
                if (string.IsNullOrWhiteSpace(input))
                {
                    UpdateStatus("请输入 JSON 内容", true);
                    return;
                }

                // 解析 JSON
                var jsonDoc = JsonDocument.Parse(input);

                // 压缩输出（不缩进）
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string minified = JsonSerializer.Serialize(jsonDoc.RootElement, options);
                TxtOutput.Text = minified;

                UpdateStatus($"压缩成功 - {minified.Length} 字符", false);
            }
            catch (JsonException ex)
            {
                TxtOutput.Text = $"JSON 错误: {ex.Message}";
                UpdateStatus($"JSON 压缩失败: {ex.Message}", true);
            }
            catch (Exception ex)
            {
                TxtOutput.Text = $"错误: {ex.Message}";
                UpdateStatus($"操作失败: {ex.Message}", true);
            }
        }

        private void BtnValidate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? input = TxtInput.Text;
                if (string.IsNullOrWhiteSpace(input))
                {
                    UpdateStatus("请输入 JSON 内容", true);
                    return;
                }

                // 验证 JSON
                var jsonDoc = JsonDocument.Parse(input);

                UpdateStatus("JSON 格式正确 ✓", false);
            }
            catch (JsonException ex)
            {
                UpdateStatus($"JSON 格式错误: {ex.Message}", true);
            }
            catch (Exception ex)
            {
                UpdateStatus($"验证失败: {ex.Message}", true);
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TxtInput.Text = string.Empty;
            TxtOutput.Text = string.Empty;
            UpdateStatus("已清空", false);
        }

        private void BtnCopyInput_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(TxtInput.Text))
                {
                    ClipboardHelper.SetText(TxtInput.Text);
                    UpdateStatus("已复制输入内容到剪贴板", false);
                }
                else
                {
                    UpdateStatus("输入内容为空", true);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"复制失败: {ex.Message}", true);
            }
        }

        private void BtnCopyOutput_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(TxtOutput.Text))
                {
                    ClipboardHelper.SetText(TxtOutput.Text);
                    UpdateStatus("已复制输出内容到剪贴板", false);
                }
                else
                {
                    UpdateStatus("输出内容为空", true);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"复制失败: {ex.Message}", true);
            }
        }

        private void UpdateStatus(string message, bool isError)
        {
            TxtStatus.Text = message;
            TxtStatus.Foreground = isError ? Brushes.Red : Brushes.Green;
        }
    }
}

