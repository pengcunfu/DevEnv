using System.Windows;
using System.Windows.Controls;
using DevEnv.Host.SigXor.Toolkit.Models;
using DevEnv.Host.SigXor.Toolkit.Ocr;
using DevEnv.Host.SigXor.Toolkit.Voice;

namespace DevEnv.Host.SigXor.Views;

/// <summary>语音 / OCR 模型管理与后台服务开关。</summary>
public partial class SigXorServiceView : UserControl
{
    private readonly VoiceInputService? _voiceService;
    private bool _modelDownloading;
    private bool _ocrModelDownloading;
    private bool _modelMigrating;
    private bool _voiceStatusSubscribed;

    public SigXorServiceView(VoiceInputService? voiceService = null)
    {
        InitializeComponent();
        _voiceService = voiceService;

        // 页签切换会触发 Loaded / Unloaded，语音服务状态订阅必须成对处理，
        // 否则切走一次后再也收不到状态更新。
        Loaded += (_, _) => SubscribeVoiceStatus();
        Unloaded += (_, _) => UnsubscribeVoiceStatus();

        RefreshServiceUi();
    }

    private void SubscribeVoiceStatus()
    {
        if (_voiceService is null || _voiceStatusSubscribed) return;
        _voiceService.StatusChanged += OnVoiceStatusChanged;
        _voiceStatusSubscribed = true;
    }

    private void UnsubscribeVoiceStatus()
    {
        if (_voiceService is null || !_voiceStatusSubscribed) return;
        _voiceService.StatusChanged -= OnVoiceStatusChanged;
        _voiceStatusSubscribed = false;
    }

    /// <summary>更新「使用说明」里的快捷键提示。</summary>
    public void SetShortcutHint(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint) || ServiceUsageHintText is null) return;
        ServiceUsageHintText.Text =
            $"使用说明：语音快捷键短按开始/再按结束，或按住说话、松手结束；截屏快捷键拖动选择区域。\n{hint}";
    }

    private void RefreshServiceUi()
    {
        bool installed = SpeechModelManager.IsInstalled;
        bool legacy = SpeechModelManager.IsUsingLegacy;
        long size = SpeechModelManager.GetSizeOnDisk();

        if (!_modelDownloading)
        {
            SpeechModelStatusText.Text = installed
                ? $"{SpeechModelManager.StatusText} · {SpeechModelManager.FormatSize(size)}"
                : "未下载（约 230 MB）";
        }

        DownloadSpeechModelButton.IsEnabled = !installed && !_modelDownloading;
        DeleteSpeechModelButton.IsEnabled = installed && !legacy && !_modelDownloading;
        DeleteSpeechModelButton.ToolTip = legacy ? "当前复用的是旧版 SigXor 目录中的模型，无法在此删除。" : null;
        OpenSpeechModelFolderButton.IsEnabled = true;

        bool ocrReady = OcrModelManager.IsReady;
        bool ocrLegacy = OcrModelManager.IsUsingLegacy;
        long ocrSize = OcrModelManager.GetSizeOnDisk();
        if (!_ocrModelDownloading)
        {
            OcrModelStatusText.Text = ocrReady
                ? $"{OcrModelManager.StatusText} · {OcrModelManager.FormatSize(ocrSize)}"
                : "未下载（约 30 MB / 回退 v5 约 16 MB）";
        }
        DownloadOcrModelButton.IsEnabled = !ocrReady && !_ocrModelDownloading;
        DeleteOcrModelButton.IsEnabled = ocrReady && !ocrLegacy && !_ocrModelDownloading;
        DeleteOcrModelButton.ToolTip = ocrLegacy ? "当前复用的是旧版 SigXor 目录中的模型，无法在此删除。" : null;
        OpenOcrModelFolderButton.IsEnabled = true;

        bool running = _voiceService?.IsRunning == true;
        bool processing = _voiceService?.IsProcessing == true;
        StartVoiceServiceButton.IsEnabled = !running && !processing && !_modelDownloading && installed;
        StopVoiceServiceButton.IsEnabled = running || processing;
        if (!_modelMigrating)
        {
            if (_voiceService is not null)
                ServiceStatusText.Text = _voiceService.Status.Message;
            else
                ServiceStatusText.Text = installed ? "语音服务未接入" : "模型未下载";
        }

        bool canMigrate = SpeechModelManager.IsUsingLegacy || OcrModelManager.IsUsingLegacy;
        MigrateLegacyModelsButton.IsEnabled = canMigrate
            && !_modelDownloading && !_ocrModelDownloading && !_modelMigrating;
    }

    private void OnVoiceStatusChanged(object? sender, VoiceServiceStatus status)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RefreshServiceUi);
            return;
        }
        RefreshServiceUi();
    }

    private async void OnStartVoiceServiceClick(object sender, RoutedEventArgs e)
    {
        if (_voiceService is null) return;
        StartVoiceServiceButton.IsEnabled = false;
        await _voiceService.StartServiceAsync();
        RefreshServiceUi();
    }

    private void OnStopVoiceServiceClick(object sender, RoutedEventArgs e)
    {
        _voiceService?.StopService();
        RefreshServiceUi();
    }

    private async void OnDownloadSpeechModelClick(object sender, RoutedEventArgs e)
    {
        _modelDownloading = true;
        SpeechDownloadProgressBar.Visibility = Visibility.Visible;
        SpeechDownloadProgressBar.IsIndeterminate = true;
        SpeechDownloadProgressBar.Value = 0;
        SpeechModelStatusText.Text = "准备下载...";
        RefreshServiceUi();

        var progress = new Progress<ModelDownloadProgress>(p =>
        {
            if (p.Percent >= 0)
            {
                SpeechDownloadProgressBar.IsIndeterminate = false;
                SpeechDownloadProgressBar.Value = p.Percent;
            }
            else
            {
                SpeechDownloadProgressBar.IsIndeterminate = true;
            }
            SpeechModelStatusText.Text = p.Message;
        });

        bool ok;
        try
        {
            ok = await SpeechModelManager.DownloadAsync(progress);
        }
        catch (OperationCanceledException)
        {
            ok = false;
            SpeechModelStatusText.Text = "已取消下载";
        }
        catch (Exception ex)
        {
            ok = false;
            SpeechModelStatusText.Text = $"下载失败：{ex.Message}";
        }
        finally
        {
            _modelDownloading = false;
            SpeechDownloadProgressBar.Visibility = Visibility.Collapsed;
            SpeechDownloadProgressBar.IsIndeterminate = false;
        }

        if (!ok)
        {
            MessageBox.Show("模型下载失败，请检查网络后重试。", "燧岚开发盒子",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshServiceUi();
    }

    private void OnDeleteSpeechModelClick(object sender, RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show(
            "删除已下载的 SenseVoice 模型？下次使用需要重新下载（约 230 MB）。",
            "燧岚开发盒子",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;

        _voiceService?.StopService();
        if (!SpeechModelManager.Delete(out string? error))
        {
            MessageBox.Show(error ?? "删除模型失败。", "燧岚开发盒子",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshServiceUi();
    }

    private void OnOpenSpeechModelFolderClick(object sender, RoutedEventArgs e)
    {
        try { SpeechModelManager.OpenFolder(); }
        catch (Exception ex)
        {
            MessageBox.Show($"打开模型目录失败：{ex.Message}", "燧岚开发盒子",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnDownloadOcrModelClick(object sender, RoutedEventArgs e)
    {
        _ocrModelDownloading = true;
        OcrDownloadProgressBar.Visibility = Visibility.Visible;
        OcrDownloadProgressBar.IsIndeterminate = true;
        OcrModelStatusText.Text = "准备下载...";
        RefreshServiceUi();

        var progress = new Progress<string>(message => OcrModelStatusText.Text = message);
        Action<string> report = ((IProgress<string>)progress).Report;
        bool ok;
        try
        {
            ok = await OcrModelManager.EnsureReadyAsync(report);
        }
        catch (OperationCanceledException)
        {
            ok = false;
            OcrModelStatusText.Text = "已取消下载";
        }
        catch (Exception ex)
        {
            ok = false;
            OcrModelStatusText.Text = $"下载失败：{ex.Message}";
        }
        finally
        {
            _ocrModelDownloading = false;
            OcrDownloadProgressBar.Visibility = Visibility.Collapsed;
        }

        if (!ok)
        {
            MessageBox.Show("OCR 模型下载失败，请检查网络后重试。", "燧岚开发盒子",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshServiceUi();
    }

    private void OnDeleteOcrModelClick(object sender, RoutedEventArgs e)
    {
        MessageBoxResult result = MessageBox.Show(
            "删除已下载的 OCR 模型？下次使用需要重新下载。",
            "燧岚开发盒子",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;

        if (!OcrModelManager.Delete(out string? error))
        {
            MessageBox.Show(error ?? "删除 OCR 模型失败。", "燧岚开发盒子",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshServiceUi();
    }

    private void OnOpenOcrModelFolderClick(object sender, RoutedEventArgs e)
    {
        try { OcrModelManager.OpenFolder(); }
        catch (Exception ex)
        {
            MessageBox.Show($"打开 OCR 模型目录失败：{ex.Message}", "燧岚开发盒子",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnMigrateLegacyModelsClick(object sender, RoutedEventArgs e)
    {
        _modelMigrating = true;
        RefreshServiceUi();
        ServiceStatusText.Text = "正在从旧版 SigXor 迁移模型...";

        var progress = new Progress<string>(message => ServiceStatusText.Text = message);
        Action<string> report = ((IProgress<string>)progress).Report;
        string result;
        try
        {
            bool speech = await SpeechModelManager.MigrateFromLegacyAsync(report);
            bool ocr = await OcrModelManager.MigrateFromLegacyAsync(report);
            result = speech || ocr ? "模型迁移完成" : "未找到可迁移的 SigXor 模型";
        }
        catch (Exception ex)
        {
            result = $"模型迁移失败：{ex.Message}";
        }
        finally
        {
            _modelMigrating = false;
            RefreshServiceUi();
        }
        ServiceStatusText.Text = result;
    }
}
