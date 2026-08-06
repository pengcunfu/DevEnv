using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ProgramBox.Models;
using ProgramBox.Utils;

namespace ProgramBox.Views
{
    public partial class ApplicationsView : UserControl
    {
        private readonly JsonDataManager _dataManager;
        private readonly ObservableCollection<NativeAtom> _allNativeApps;
        private readonly ObservableCollection<NativeAtom> _filteredNativeApps;
        private readonly ObservableCollection<WebAtom> _allWebApps;
        private readonly ObservableCollection<WebAtom> _filteredWebApps;
        private readonly ObservableCollection<FolderAtom> _allFolders;
        private readonly ObservableCollection<FolderAtom> _filteredFolders;

        public ApplicationsView(JsonDataManager dataManager)
        {
            InitializeComponent();
            _dataManager = dataManager;

            _allNativeApps = new ObservableCollection<NativeAtom>(_dataManager.Data.NativeAtomList);
            _filteredNativeApps = new ObservableCollection<NativeAtom>(_allNativeApps);

            _allFolders = new ObservableCollection<FolderAtom>(_dataManager.Data.FolderAtomList);
            _filteredFolders = new ObservableCollection<FolderAtom>(_allFolders);

            _allWebApps = new ObservableCollection<WebAtom>(_dataManager.Data.WebAtomList);
            _filteredWebApps = new ObservableCollection<WebAtom>(_allWebApps);

            SetupDragDrop();
            LoadApplications();
        }

        private void SetupDragDrop()
        {
            DragDrop.SetAllowDrop(DropZone, true);
            DropZone.AddHandler(DragDrop.DragOverEvent, OnNativeDragOver, RoutingStrategies.Bubble);
            DropZone.AddHandler(DragDrop.DropEvent, OnNativeDrop, RoutingStrategies.Bubble);

            DragDrop.SetAllowDrop(FolderDropZone, true);
            FolderDropZone.AddHandler(DragDrop.DragOverEvent, OnFolderDragOver, RoutingStrategies.Bubble);
            FolderDropZone.AddHandler(DragDrop.DropEvent, OnFolderDrop, RoutingStrategies.Bubble);
        }

        private void LoadApplications()
        {
            NativeApplicationsList.ItemsSource = _filteredNativeApps;
            FolderShortcutsList.ItemsSource = _filteredFolders;
            WebApplicationsList.ItemsSource = _filteredWebApps;
            UpdateEmptyStates();
        }

        private void UpdateEmptyStates()
        {
            NativeEmptyState.IsVisible = _filteredNativeApps.Count == 0;
            FolderEmptyState.IsVisible = _filteredFolders.Count == 0;
            WebEmptyState.IsVisible = _filteredWebApps.Count == 0;
        }

        #region 拖放与添加

        private void OnNativeDragOver(object? sender, DragEventArgs e)
        {
            if (HasExecutableFiles(e))
            {
                e.DragEffects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private async void OnNativeDrop(object? sender, DragEventArgs e)
        {
            e.Handled = true;
            var paths = await GetExecutablePathsFromDragAsync(e);
            foreach (var path in paths)
                await AddNativeAppFromExeAsync(path);
        }

        private static bool HasExecutableFiles(DragEventArgs e)
        {
            if (!e.Data.Contains(DataFormats.Files))
                return false;

            return e.Data.GetFiles()?.Any(f =>
                f.Path.LocalPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) == true;
        }

        private static async System.Threading.Tasks.Task<string[]> GetExecutablePathsFromDragAsync(DragEventArgs e)
        {
            if (!e.Data.Contains(DataFormats.Files))
                return Array.Empty<string>();

            var files = e.Data.GetFiles();
            if (files == null)
                return Array.Empty<string>();

            var list = new System.Collections.Generic.List<string>();
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;
                if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                    list.Add(path);
            }

            return list.ToArray();
        }

        private async void BrowseExeButton_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择可执行程序",
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType("可执行文件") { Patterns = ["*.exe"] }
                ]
            });

            foreach (var file in files)
                await AddNativeAppFromExeAsync(file.Path.LocalPath);
        }

        private void AddApplicationButton_Click(object? sender, RoutedEventArgs e)
            => BrowseExeButton_Click(sender, e);

        private async System.Threading.Tasks.Task AddNativeAppFromExeAsync(string exePath)
        {
            try
            {
                var atom = NativeAppHelper.CreateFromExecutable(exePath);
                if (atom == null)
                {
                    await AppHelper.ShowErrorAsync("无法添加该文件，请选择有效的 .exe 程序。");
                    return;
                }

                var existing = _dataManager.FindNativeByExecPath(atom.ExecPath);
                if (existing != null)
                {
                    existing.Name = atom.Name;
                    existing.IconPath = atom.IconPath;
                    existing.Description = atom.ExecPath;
                    _dataManager.Save();
                    await AppHelper.ShowInfoAsync($"已更新：{existing.Name}");
                    return;
                }

                _dataManager.AddNativeAtom(atom);
                _allNativeApps.Add(atom);
                _filteredNativeApps.Add(atom);
                UpdateEmptyStates();
                await AppHelper.ShowInfoAsync($"已添加：{atom.Name}");
            }
            catch (Exception ex)
            {
                await AppHelper.ShowErrorAsync($"添加应用失败: {ex.Message}");
            }
        }

        #endregion

        #region 文件夹快捷方式

        private void OnFolderDragOver(object? sender, DragEventArgs e)
        {
            if (HasDirectories(e))
            {
                e.DragEffects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private async void OnFolderDrop(object? sender, DragEventArgs e)
        {
            e.Handled = true;
            foreach (var path in await GetDirectoryPathsFromDragAsync(e))
                await AddFolderFromPathAsync(path);
        }

        private static bool HasDirectories(DragEventArgs e)
        {
            if (!e.Data.Contains(DataFormats.Files))
                return false;

            return e.Data.GetFiles()?.Any(f => Directory.Exists(f.Path.LocalPath)) == true;
        }

        private static async System.Threading.Tasks.Task<string[]> GetDirectoryPathsFromDragAsync(DragEventArgs e)
        {
            if (!e.Data.Contains(DataFormats.Files))
                return Array.Empty<string>();

            var files = e.Data.GetFiles();
            if (files == null)
                return Array.Empty<string>();

            var list = new System.Collections.Generic.List<string>();
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;
                if (Directory.Exists(path))
                    list.Add(path);
            }

            return list.ToArray();
        }

        private async void BrowseFolderButton_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择文件夹",
                AllowMultiple = true
            });

            foreach (var folder in folders)
                await AddFolderFromPathAsync(folder.Path.LocalPath);
        }

        private async System.Threading.Tasks.Task AddFolderFromPathAsync(string folderPath)
        {
            try
            {
                var atom = FolderShortcutHelper.CreateFromDirectory(folderPath);
                if (atom == null)
                {
                    await AppHelper.ShowErrorAsync("无法添加该路径，请选择有效的文件夹。");
                    return;
                }

                var existing = _dataManager.FindFolderByPath(atom.FolderPath);
                if (existing != null)
                {
                    existing.IconPath = string.IsNullOrEmpty(atom.IconPath) ? existing.IconPath : atom.IconPath;
                    existing.Description = atom.FolderPath;
                    _dataManager.Save();
                    await AppHelper.ShowInfoAsync($"已存在，路径已更新：{existing.Name}");
                    return;
                }

                _dataManager.AddFolderAtom(atom);
                _allFolders.Add(atom);
                _filteredFolders.Add(atom);
                UpdateEmptyStates();
                await AppHelper.ShowInfoAsync($"已添加：{atom.Name}");
            }
            catch (Exception ex)
            {
                await AppHelper.ShowErrorAsync($"添加文件夹失败: {ex.Message}");
            }
        }

        private void FolderCard_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source is Button)
                return;

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            if (sender is Border { Tag: FolderAtom folder })
            {
                OpenFolder(folder);
                e.Handled = true;
            }
        }

        private void OpenFolder_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: FolderAtom folder })
                OpenFolder(folder);
        }

        private async void EditFolder_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: FolderAtom folder })
                return;

            var owner = TopLevel.GetTopLevel(this) as Window;
            var dialog = new FolderEditWindow(folder);
            if (owner != null)
                await dialog.ShowDialog(owner);
            else
                dialog.Show();

            if (dialog.Saved)
            {
                _dataManager.Save();
                RefreshFolderList();
            }
        }

        private void RefreshFolderList()
        {
            var snapshot = _filteredFolders.ToList();
            _filteredFolders.Clear();
            foreach (var item in snapshot)
                _filteredFolders.Add(item);
        }

        private async void DeleteFolder_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: FolderAtom folder })
                return;

            IconCacheHelper.TryDeleteCachedIcon(folder.IconPath);
            _dataManager.RemoveFolderAtom(folder);
            _allFolders.Remove(folder);
            _filteredFolders.Remove(folder);
            UpdateEmptyStates();
            await AppHelper.ShowInfoAsync($"已移除 {folder.Name}");
        }

        private void OpenFolder(FolderAtom folder)
        {
            if (!Directory.Exists(folder.FolderPath))
            {
                _ = AppHelper.ShowErrorAsync($"文件夹不存在: {folder.FolderPath}");
                return;
            }

            AppHelper.OpenInExplorer(folder.FolderPath);
        }

        #endregion

        #region 本地应用

        private void NativeAppCard_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source is Button)
                return;

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            if (sender is Border { Tag: NativeAtom app })
            {
                RunApplication(app);
                e.Handled = true;
            }
        }

        private void RunApplication_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: NativeAtom app })
                RunApplication(app);
        }

        private void ShowInExplorer_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: NativeAtom app })
                AppHelper.OpenInExplorer(app.ExecPath);
        }

        private async void DeleteNativeApplication_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: NativeAtom app })
                return;

            NativeAppHelper.TryDeleteCachedIcon(app.IconPath);
            _dataManager.RemoveNativeAtom(app);
            _allNativeApps.Remove(app);
            _filteredNativeApps.Remove(app);
            UpdateEmptyStates();
            await AppHelper.ShowInfoAsync($"已移除 {app.Name}");
        }

        private void RunApplication(NativeAtom app)
        {
            if (!File.Exists(app.ExecPath))
            {
                _ = AppHelper.ShowErrorAsync($"找不到应用程序: {app.ExecPath}");
                return;
            }

            AppHelper.LaunchExecutable(app.ExecPath);
        }

        #endregion

        #region 网站快捷方式

        private void WebCard_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source is Button)
                return;

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;

            if (sender is Border { Tag: WebAtom app })
            {
                OpenWebApplication(app);
                e.Handled = true;
            }
        }

        private void OpenWebApplication_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: WebAtom app })
                OpenWebApplication(app);
        }

        private async void EditWebApplication_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: WebAtom app })
                return;

            var owner = TopLevel.GetTopLevel(this) as Window;
            var dialog = new WebEditWindow(app);
            if (owner != null)
                await dialog.ShowDialog(owner);
            else
                dialog.Show();

            if (dialog.Saved)
            {
                _dataManager.Save();
                RefreshWebList();
            }
        }

        private void RefreshWebList()
        {
            var snapshot = _filteredWebApps.ToList();
            _filteredWebApps.Clear();
            foreach (var item in snapshot)
                _filteredWebApps.Add(item);
        }

        private async void DeleteWebApplication_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: WebAtom app })
                return;

            await DeleteWebApplication(app);
        }

        private async void AddWebApplicationButton_Click(object? sender, RoutedEventArgs e)
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            var dialog = new WebAddWindow();
            if (owner != null)
                await dialog.ShowDialog(owner);
            else
                dialog.Show();

            if (dialog.Result == null)
                return;

            await AddWebFromMetadataAsync(dialog.Result);
        }

        private async System.Threading.Tasks.Task AddWebFromMetadataAsync(WebMetadata meta)
        {
            try
            {
                var existing = _dataManager.FindWebByUrl(meta.Url);
                if (existing != null)
                {
                    existing.Name = meta.Title;
                    if (!string.IsNullOrEmpty(meta.IconPath))
                        existing.IconPath = meta.IconPath;
                    existing.Description = meta.Url;
                    _dataManager.Save();
                    await AppHelper.ShowInfoAsync($"已更新：{existing.Name}");
                    RefreshWebList();
                    return;
                }

                var atom = new WebAtom(meta.Title, meta.IconPath, "web", meta.Url, meta.Url);
                _dataManager.AddWebAtom(atom);
                _allWebApps.Add(atom);
                _filteredWebApps.Add(atom);
                UpdateEmptyStates();
                await AppHelper.ShowInfoAsync($"已添加：{atom.Name}");
            }
            catch (Exception ex)
            {
                await AppHelper.ShowErrorAsync($"添加网站失败: {ex.Message}");
            }
        }

        private void OpenWebApplication(WebAtom app)
        {
            try
            {
                var url = WebShortcutHelper.NormalizeUrl(app.Url) ?? app.Url;
                AppHelper.OpenInBrowser(url);
            }
            catch (Exception ex)
            {
                _ = AppHelper.ShowErrorAsync($"打开网站失败: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task DeleteWebApplication(WebAtom app)
        {
            IconCacheHelper.TryDeleteCachedIcon(app.IconPath);
            _dataManager.RemoveWebAtom(app);
            _allWebApps.Remove(app);
            _filteredWebApps.Remove(app);
            UpdateEmptyStates();
            await AppHelper.ShowInfoAsync($"已移除 {app.Name}");
        }

        #endregion
    }
}
