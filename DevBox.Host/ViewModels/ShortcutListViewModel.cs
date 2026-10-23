using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevBox.Core.Services;

namespace DevBox.Host.ViewModels;

/// <summary>三类快捷方式列表页的公共部分：状态提示、单击/双击策略、刷新。</summary>
public abstract partial class ShortcutListViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>卡片单击时调用；clickCount 为 1 表示单击、2 表示双击。</summary>
    public abstract void OnCardActivated(object item, int clickCount);

    /// <summary>单击 / 双击启动策略：设置项「双击启动快捷方式」打开时只有双击才启动。</summary>
    protected bool RequireDoubleClick => CoreServices.Settings.GetConfig().DoubleClickToLaunch;

    /// <summary>是否应该按这次点击启动。</summary>
    protected bool ShouldLaunch(int clickCount)
    {
        var requireDouble = RequireDoubleClick;
        return requireDouble ? clickCount >= 2 : clickCount == 1;
    }

    /// <summary>重新拉取列表（派生类实现）。命令入口固定在基类，避免 override 上重复标注。</summary>
    public abstract void Reload();

    /// <summary>命令入口。方法名不要带 Command 后缀，否则生成器会拼成 LoadItemsCommandCommand。</summary>
    [RelayCommand]
    private void LoadItems() => Reload();

    protected void Report(DevBox.Core.Models.OperationResult result)
    {
        if (!result.Ok)
            Message = result.DisplayMessage;
    }
}