using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时 UI 画布提供器。
/// 
/// 新手理解：
/// 1. 很多 UI 脚本会在运行时自动创建文字、按钮、面板。
/// 2. 创建这些 UI 前必须先有 Canvas，否则 UI 不会显示。
/// 3. 这个类负责“找一个合适的 Canvas；找不到就自动新建一个”。
/// 4. static class 不能挂在物体上，它只是给其他脚本调用的工具箱。
/// </summary>
public static class RuntimeUiCanvasProvider
{
    // 自动创建的 Canvas 名字。之后再找 Canvas 时可以通过名字识别它。
    private const string RuntimeCanvasName = "RuntimeOverlayCanvas";

    // 排序值越高越靠前显示；5000 基本能盖在普通 UI 上面。
    private const int RuntimeCanvasSortingOrder = 5000;

    /// <summary>
    /// 找到或创建一个可用于运行时 UI 的屏幕空间 Canvas。
    /// </summary>
    public static Canvas GetOrCreateCanvas()
    {
        Canvas existingCanvas = FindBestCanvas();
        if (existingCanvas != null)
        {
            EnsureCanvasSetup(existingCanvas);
            return existingCanvas;
        }

        GameObject canvasObject = new GameObject(
            RuntimeCanvasName,
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        EnsureCanvasSetup(canvas);
        return canvas;
    }

    /// <summary>
    /// 从场景里找最适合复用的 Canvas。
    /// 优先顺序：自动创建的 RuntimeOverlayCanvas -> 名字叫 Canvas 的主画布 -> 任意屏幕空间画布。
    /// </summary>
    public static Canvas FindBestCanvas()
    {
        Canvas runtimeCanvas = FindNamedRuntimeCanvas();
        if (runtimeCanvas != null)
        {
            return runtimeCanvas;
        }

        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        Canvas fallbackCanvas = null;

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                continue;
            }

            // Unity 默认 UI 画布常叫 Canvas，如果存在就优先复用，方便在层级面板里调 UI。
            if (canvas.name == "Canvas")
            {
                return canvas;
            }

            if (fallbackCanvas == null)
            {
                fallbackCanvas = canvas;
            }
        }

        return fallbackCanvas;
    }

    /// <summary>
    /// 查找之前由本工具自动创建的 Canvas。
    /// </summary>
    private static Canvas FindNamedRuntimeCanvas()
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].name == RuntimeCanvasName)
            {
                return canvases[i];
            }
        }

        return null;
    }

    /// <summary>
    /// 统一配置 Canvas、CanvasScaler、GraphicRaycaster，保证自动 UI 可见且可点击。
    /// </summary>
    private static void EnsureCanvasSetup(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        // ScreenSpaceOverlay 表示 UI 直接覆盖在屏幕上，不需要摄像机渲染。
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = RuntimeCanvasSortingOrder;
        canvas.overrideSorting = true;
        canvas.pixelPerfect = false;

        // CanvasScaler 让 UI 在不同分辨率下按 1920x1080 的参考尺寸缩放。
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // GraphicRaycaster 让按钮等 UI 能接收点击事件。
        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }
}
