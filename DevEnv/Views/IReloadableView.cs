namespace DevEnv.Host.Views;

/// <summary>可刷新页面：主窗拖放添加快捷方式后，当前页需要重新拉数据。</summary>
public interface IReloadableView
{
    void Reload();
}