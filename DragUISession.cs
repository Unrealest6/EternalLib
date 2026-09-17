namespace EternalLib
{
    /// <summary>
    /// 面板拖拽的全局会话状态。
    /// <para>用法：宿主订阅 <see cref="DragStarted"/> 取消自己的拖拽操作，或在每帧检查
    /// <see cref="IsAnyPanelDragging"/> 直接跳过输入处理。</para>
    /// </summary>
    public static class DragUISession
    {
        /// <summary>当前正在被拖拽的面板数量。</summary>
        public static int ActiveDragCount { get; private set; }
        /// <summary>是否有面板正在被拖拽。</summary>
        public static bool IsAnyPanelDragging => ActiveDragCount > 0;
        /// <summary>面板开始被拖拽时触发（宿主可在此取消自己进行中的拖拽）。</summary>
        public static event Action? DragStarted;
        /// <summary>最后一个面板拖拽结束时触发。</summary>
        public static event Action? DragEnded;
        internal static void NotifyDragStarted()
        {
            ActiveDragCount++;
            DragStarted?.Invoke();
        }
        /// <summary>
        /// 丢弃指定界面的鼠标按下缓存，使进行中的拖拽在松手时不会再补发点击/双击。
        /// </summary>
        public static void DiscardPendingClicks(UserInterface? ui) => ui?.ClearPointers();
        internal static void NotifyDragEnded()
        {
            if (ActiveDragCount > 0)
            {
                ActiveDragCount--;
            }
            if (ActiveDragCount == 0)
            {
                DragEnded?.Invoke();
            }
        }
        /// <summary>重置会话状态（模组卸载时调用；不清空事件会让宿主程序集无法卸载）。</summary>
        internal static void Reset()
        {
            ActiveDragCount = 0;
            DragStarted = null;
            DragEnded = null;
        }
    }
}