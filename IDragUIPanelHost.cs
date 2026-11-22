namespace EternalLib
{
    /// <summary>
    /// 面板宿主的契约：任何持有 <see cref="DragPanelHost"/> 的类（通常是 tModLoader 的 <see cref="ModSystem"/>）实现它即可，
    /// 调用方只认接口，不再被“系统泛型 + CRTP 自约束”绑住。
    /// </summary>
    public interface IDragUIPanelHost
    {
        /// <summary>组合进来的面板宿主（状态与行为都在里面）。</summary>
        DragPanelHost Panels { get; }
    }
}
