# 淘宝 UI PSD 导出工具

该工具把 `Assets/AllResources/淘宝ui素材` 中的分层 PSD 转换为 Unity 可直接使用的透明 PNG。
源 PSD 和预览图不会被移动或删除，运行时图片统一输出到 `RuntimeSprites`。

## 环境准备

```powershell
python -m pip install -r Tools/UiAssetPipeline/requirements.txt
```

## 使用顺序

```powershell
# 仅在首次导入或需要重新分析 PSD 图层结构时执行。
python Tools/UiAssetPipeline/export_ui_assets.py discover

# 检查 49 个 PSD、图层索引、命名、分类和九宫格数据，不生成图片。
python Tools/UiAssetPipeline/export_ui_assets.py dry-run

# 按已经确认的显式清单重新导出，并清理旧的生成结果。
python Tools/UiAssetPipeline/export_ui_assets.py export --clean
```

`export_manifest.json` 是显式导出清单。清单存在后，普通重新导出不要再次执行
`discover`，否则会覆盖人工确认过的图层选择。确实需要重建时使用 `discover --force`，
并重新检查分类预览图。

## 命名与拆分规则

- 文件名和 Unity Sprite 名统一为 `UI_<Category>_<Element>_<Variant>_<State>`。
- 运行时 PNG 不保留普通英文文本和示例数值，文字由 TextMeshPro 显示。
- 按钮内部的渐变、描边和阴影合并为一张底图，按钮文字不烘焙。
- Slider 拆为 `Track / Fill / Handle`。
- 血条拆为 `Background / Fill`，Boss、MiniBoss 和 Default 分别导出。
- 九宫格 Border 写入 `PurchasedUiSpriteImportRules.json`，由 Unity Editor 导入器应用。
- 同像素、同分类、同角色、同原图层语义的重复项只保留一份 PNG，目录中记录别名。

## Unity 中使用

导出完成后打开 Unity，等待自动导入；也可以执行：

- `Tools/Treasure Hunter/UI Assets/Apply Import Settings`
- `Tools/Treasure Hunter/UI Assets/Validate Library`

导入器只处理淘宝 UI 专用目录，不会改变项目其他贴图。
