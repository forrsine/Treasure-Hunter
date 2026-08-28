#!/usr/bin/env python3
"""
淘宝 UI PSD 离线导出工具。

这个脚本只负责美术源文件到运行时 PNG 的转换，不修改场景、Prefab 或业务脚本。
首次使用 discover 固化显式清单，后续 dry-run/export 都严格以清单为准，避免 PSD
图层顺序改变后静默覆盖成错误图片。
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import shutil
import sys
import unicodedata
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence

try:
    from PIL import Image, ImageDraw, ImageFont
    from psd_tools import PSDImage
except ImportError as exc:
    raise SystemExit(
        "缺少 PSD 导出依赖。请先执行：python -m pip install -r "
        "Tools/UiAssetPipeline/requirements.txt"
    ) from exc


PROJECT_ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = PROJECT_ROOT / "Assets" / "AllResources" / "淘宝ui素材"
OUTPUT_ROOT = SOURCE_ROOT / "RuntimeSprites"
MANIFEST_PATH = Path(__file__).resolve().parent / "export_manifest.json"
IMPORT_RULES_PATH = (
    PROJECT_ROOT / "Assets" / "Editor" / "PurchasedUiSpriteImportRules.json"
)
CATALOG_CSV_PATH = PROJECT_ROOT / "Docs" / "UiAssetCatalog.csv"
CATALOG_MD_PATH = PROJECT_ROOT / "Docs" / "UiAssetCatalog.md"
PREVIEW_ROOT = PROJECT_ROOT / "Docs" / "UiAssetCatalog" / "Previews"

EXPECTED_PSD_COUNT = 49
MANIFEST_VERSION = 1
FUNCTION_ICON_CANVAS_SIZE = (104, 104)
OUTPUT_NAME_PATTERN = re.compile(r"^UI_[A-Za-z0-9]+(?:_[A-Za-z0-9]+)*$")

CATEGORY_BY_SOURCE = {
    "Buttons": "Common",
    "Component": "Common",
    "CommonMessage": "Common",
    "ErrorNetwork": "Common",
    "LoadingBar": "Common",
    "FunctionIcons": "FunctionIcons",
    "Login": "Auth",
    "Title": "Auth",
    "TitleStart": "Auth",
    "PopupChecking": "Popups",
    "PopupEUALPrivacy": "Popups",
    "PopupLogin": "Popups",
    "PopupName": "Popups",
    "PopupSignup": "Popups",
    "PopupUpdate": "Popups",
    "Language": "Popups",
    "Home": "Home",
    "Setting": "Home",
    "PowerSave": "Home",
    "Offline": "Home",
    "Rate": "Home",
    "ADRemove": "Home",
    "Character": "Character",
    "CharacterSelect": "Character",
    "CharacterTutorialChat": "Character",
    "Equipment": "Equipment",
    "EquipmentDetail1": "Equipment",
    "EquipmentDetail2": "Equipment",
    "EquipmentDetail3": "Equipment",
    "BattlePass": "Progression",
    "GuideBook": "Progression",
    "Guild": "Progression",
    "LevelUp": "Progression",
    "Missions": "Progression",
    "Ranking": "Progression",
    "RewardDaily": "Progression",
    "RewardWeek": "Progression",
    "Roulette": "Progression",
    "StageSelect": "Progression",
    "Shop_Chest": "Shop",
    "Shop_Gem": "Shop",
    "Shop_Gold": "Shop",
    "PlayBoss": "Gameplay",
    "PlayContinue": "Gameplay",
    "PlayPause": "Gameplay",
    "PlayResult": "Gameplay",
    "PlayType1": "Gameplay",
    "PlayType2": "Gameplay",
    "PlayType3": "Gameplay",
}

CATEGORY_CHINESE = {
    "Common": "通用控件",
    "FunctionIcons": "功能图标",
    "Auth": "登录与标题",
    "Popups": "通用弹窗",
    "Home": "主界面",
    "Character": "角色界面",
    "Equipment": "装备界面",
    "Progression": "成长与活动",
    "Shop": "商店界面",
    "Gameplay": "战斗界面",
}

TOKEN_CHINESE = {
    "account": "账号信息",
    "ad": "广告",
    "add": "增加",
    "arrow": "箭头",
    "auto": "自动",
    "back": "返回",
    "background": "背景",
    "badge": "角标",
    "bar": "进度条",
    "battle": "战斗",
    "blue": "蓝色",
    "boss": "Boss",
    "brown": "棕色",
    "button": "按钮",
    "btn": "按钮底图",
    "cancel": "取消",
    "character": "角色",
    "check": "勾选",
    "checkbox": "复选框",
    "chest": "宝箱",
    "circle": "圆形",
    "coin": "金币",
    "common": "通用",
    "continue": "继续",
    "control": "控制",
    "currency": "货币",
    "daily": "每日",
    "dark": "深色",
    "deco": "装饰",
    "default": "默认",
    "detail": "详情",
    "disabled": "禁用",
    "energy": "体力",
    "equipment": "装备",
    "error": "错误",
    "field": "输入框",
    "fill": "填充",
    "focus": "选中高亮",
    "frame": "边框",
    "gem": "宝石",
    "gold": "金币",
    "gray": "灰色",
    "green": "绿色",
    "guild": "公会",
    "heroes": "英雄",
    "handle": "滑块",
    "health": "生命值",
    "home": "主页",
    "hp": "生命值",
    "icon": "图标",
    "input": "输入框",
    "inventory": "背包",
    "img": "图片",
    "item": "物品",
    "label": "标签",
    "level": "等级",
    "loading": "加载",
    "lock": "锁定",
    "menu": "菜单",
    "message": "消息",
    "mint": "薄荷色",
    "mission": "任务",
    "name": "名称",
    "normal": "普通状态",
    "notification": "通知",
    "off": "关闭状态",
    "on": "开启状态",
    "orange": "橙色",
    "overlay": "覆盖层",
    "page": "页面",
    "panel": "面板",
    "pause": "暂停",
    "play": "开始游戏",
    "popup": "弹窗",
    "progress": "进度",
    "purple": "紫色",
    "ranking": "排行榜",
    "red": "红色",
    "resource": "资源栏",
    "result": "结算",
    "reward": "奖励",
    "selected": "选中状态",
    "setting": "设置",
    "shop": "商店",
    "silver": "银色",
    "skill": "技能",
    "slot": "槽位",
    "stage": "关卡",
    "stat": "属性",
    "store": "商店",
    "switch": "开关",
    "tab": "页签",
    "title": "标题",
    "track": "轨道",
    "upgrade": "升级",
    "week": "每周",
    "white": "白色",
    "yellow": "黄色",
}

CONTAINER_NAMES = {
    "arrow",
    "buttons",
    "buttons1",
    "buttons2",
    "characterlevel",
    "commonbuttons",
    "commonuielements",
    "equipmentgrade",
    "icons",
    "item",
    "items",
    "menu",
    "playbtn",
    "resource",
    "reward",
    "stage",
    "submenu",
    "tab",
    "textinput",
    "top",
}

DYNAMIC_NAMES = {
    "bosshp",
    "controlbar",
    "defaulthp",
    "gage",
    "healthbar",
    "hpboss",
    "hpdefault",
    "hpbar",
    "hpminiboss",
    "loadingbar",
    "minibosshp",
    "pageprogress",
    "progress",
    "stageprogress",
}

ROLE_NAME_PARTS = {
    "background": "Background",
    "bg": "Background",
    "fill": "Fill",
    "bar": "Fill",
    "prg": "Fill",
    "frame": "Frame",
    "handle": "Handle",
    "pointer": "Handle",
    "point": "Overlay",
    "switch": "Handle",
    "track": "Track",
    "icon": "Icon",
    "focus": "Selected",
    "selected": "Selected",
    "disabled": "Disabled",
    "off": "Off",
    "on": "On",
    "deco": "Overlay",
    "shadow": "Overlay",
}

SKIP_GROUP_NAMES = {
    "text",
    "txt",
    "menutext",
}

RASTER_TEXT_NAMES_BY_SOURCE = {
    "Login": {"fantasy", "rpg"},
    "Title": {"fantasy", "rpg", "title"},
    "TitleStart": {"fantasy", "rpg", "title"},
}


@dataclass(frozen=True)
class LayerReference:
    indices: tuple[int, ...]
    display_path: str


def normalize_name(value: str) -> str:
    return re.sub(r"[^a-z0-9]+", "", value.lower())


def split_words(value: str) -> list[str]:
    value = re.sub(r"([a-z0-9])([A-Z])", r"\1 \2", value)
    value = value.replace("&", " And ")
    return [part for part in re.split(r"[^A-Za-z0-9]+", value) if part]


def pascal_token(value: str) -> str:
    ascii_value = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode()
    if re.fullmatch(
        r"\s*(group|shape|layer|ellipse|rounded\s+rectangle|vector\s+smart\s+object)\s*\d*(?:\s+copy\s*\d*)?\s*",
        ascii_value,
        flags=re.IGNORECASE,
    ):
        return "Artwork"
    words = split_words(ascii_value)
    result = "".join(word[:1].upper() + word[1:].lower() for word in words)
    if result and result[0].isdigit():
        result = "Item" + result.zfill(2)
    return result or "Element"


def canonical_segment(layer: Any, sibling_index: int) -> str:
    occurrence = 0
    if layer.parent is not None:
        for candidate in layer.parent:
            if candidate is layer:
                break
            if candidate.name == layer.name:
                occurrence += 1
    suffix = f"#{occurrence + 1}" if occurrence else ""
    return f"{layer.name}{suffix}"


def layer_reference(layer: Any) -> LayerReference:
    indices: list[int] = []
    segments: list[str] = []
    current = layer
    while current.parent is not None:
        parent = current.parent
        index = next(index for index, child in enumerate(parent) if child is current)
        indices.append(index)
        segments.append(canonical_segment(current, index))
        current = parent
    return LayerReference(tuple(reversed(indices)), "/".join(reversed(segments)))


def resolve_layer(psd: Any, indices: Sequence[int]) -> Any:
    current = psd
    for index in indices:
        current = current[index]
    return current


def bbox_size(layer: Any) -> tuple[int, int]:
    x1, y1, x2, y2 = layer.bbox
    return max(0, x2 - x1), max(0, y2 - y1)


def is_renderable(layer: Any) -> bool:
    width, height = bbox_size(layer)
    return layer.is_visible() and width > 0 and height > 0 and layer.kind != "type"


def has_non_text_art(layer: Any) -> bool:
    if not layer.is_group():
        return is_renderable(layer)
    return any(has_non_text_art(child) for child in layer)


def renderable_group_children(layer: Any) -> list[Any]:
    return [
        child
        for child in layer
        if child.is_group()
        and is_renderable(child)
        and normalize_name(child.name) not in SKIP_GROUP_NAMES
        and has_non_text_art(child)
    ]


def boxes_are_separate(children: Sequence[Any]) -> bool:
    if len(children) < 3:
        return False
    overlaps = 0
    comparisons = 0
    for index, left in enumerate(children):
        lx1, ly1, lx2, ly2 = left.bbox
        left_area = max(1, (lx2 - lx1) * (ly2 - ly1))
        for right in children[index + 1 :]:
            rx1, ry1, rx2, ry2 = right.bbox
            right_area = max(1, (rx2 - rx1) * (ry2 - ry1))
            intersection = max(0, min(lx2, rx2) - max(lx1, rx1)) * max(
                0, min(ly2, ry2) - max(ly1, ry1)
            )
            comparisons += 1
            if intersection / min(left_area, right_area) > 0.25:
                overlaps += 1
    return comparisons > 0 and overlaps / comparisons < 0.2


def should_split_group(layer: Any, depth: int) -> bool:
    if depth >= 3:
        return False
    normalized = normalize_name(layer.name)
    children = renderable_group_children(layer)
    if not children:
        return False
    if normalized.isdigit() or normalized in CONTAINER_NAMES:
        return True
    if any(token in normalized for token in ("items", "icons", "buttons", "options")):
        return True
    return len(children) >= 4 and boxes_are_separate(children)


def is_button_group(layer: Any) -> bool:
    normalized = normalize_name(layer.name)
    return normalized.startswith("button") or normalized.startswith("btn")


def special_button_parts(layer: Any) -> list[Any]:
    if not layer.is_group() or not is_button_group(layer):
        return []
    children = [child for child in layer if is_renderable(child)]
    button_bases = [child for child in children if normalize_name(child.name) in {"btn", "button"}]
    if not button_bases:
        return []
    result = list(button_bases)
    for child in children:
        normalized = normalize_name(child.name)
        if child in button_bases or child.kind == "type" or normalized in SKIP_GROUP_NAMES:
            continue
        if child.is_group() or normalized.startswith(("icon", "deco")):
            result.append(child)
    return result


def dynamic_parts(layer: Any) -> list[Any]:
    normalized = normalize_name(layer.name)
    if not any(dynamic in normalized for dynamic in DYNAMIC_NAMES):
        return []

    # 通用 Slider 的 prg 由轨道和填充两个 Shape 组成，pointer 是可拖动滑块。
    if normalized == "controlbar":
        control = next(
            (child for child in layer if child.is_group() and normalize_name(child.name) == "control"),
            None,
        )
        if control is not None:
            progress = next(
                (child for child in control if child.is_group() and normalize_name(child.name) == "prg"),
                None,
            )
            pointer = next(
                (child for child in control if child.is_group() and normalize_name(child.name) == "pointer"),
                None,
            )
            progress_shapes = [child for child in progress] if progress is not None else []
            if len(progress_shapes) >= 2 and pointer is not None:
                return [progress_shapes[0], progress_shapes[1], pointer]

    # 章节进度条把轨道、填充与关卡点分开，运行时才能独立更新进度。
    if normalized == "stageprogress":
        bar_container = next(
            (child for child in layer if child.is_group() and normalize_name(child.name) == "bar"),
            None,
        )
        if bar_container is not None:
            bar = next(
                (child for child in bar_container if child.is_group() and normalize_name(child.name) == "bar"),
                None,
            )
            points = next(
                (child for child in bar_container if child.is_group() and normalize_name(child.name) == "point"),
                None,
            )
            bar_shapes = [child for child in bar] if bar is not None else []
            if len(bar_shapes) >= 2 and points is not None:
                return [bar_shapes[0], bar_shapes[1], points]

    semantic = {
        "bg", "background", "bar", "barbg", "fill", "frame", "handle", "prg", "prgbg", "track"
    }
    direct = [
        child
        for child in layer
        if is_renderable(child) and normalize_name(child.name) in semantic
    ]
    if len(direct) >= 2:
        return direct

    nested: list[Any] = []
    for child in layer:
        if not child.is_group() or not is_renderable(child):
            continue
        nested.extend(
            grandchild
            for grandchild in child
            if is_renderable(grandchild) and normalize_name(grandchild.name) in semantic
        )
    return nested if len(nested) >= 2 else []


def discover_layers(layer: Any, depth: int = 0) -> list[Any]:
    if not is_renderable(layer) or normalize_name(layer.name) in SKIP_GROUP_NAMES:
        return []
    if not layer.is_group():
        return [layer]

    button_parts = special_button_parts(layer)
    if button_parts:
        return button_parts

    parts = dynamic_parts(layer)
    if parts:
        return parts

    if should_split_group(layer, depth):
        result: list[Any] = []
        for child in renderable_group_children(layer):
            result.extend(discover_layers(child, depth + 1))
        return result or [layer]
    return [layer]


def significant_path_tokens(source_stem: str, layer: Any) -> list[str]:
    reference = layer_reference(layer)
    segments = [segment.split("#", 1)[0] for segment in reference.display_path.split("/")]
    result: list[str] = []
    for segment in segments:
        normalized = normalize_name(segment)
        if not normalized or normalized in {"group", "group1", "group2"}:
            continue
        if normalized.isdigit():
            result.append(f"Slot{int(normalized):02d}")
            continue
        token = pascal_token(segment)
        if not result or normalize_name(result[-1]) != normalize_name(token):
            result.append(token)

    source_token = pascal_token(source_stem)
    if result and normalize_name(result[0]) == normalize_name(source_token):
        result.pop(0)
    return result[-4:]


def infer_role(source_stem: str, layer: Any) -> str:
    if source_stem == "FunctionIcons":
        return "Simple"
    normalized = normalize_name(layer.name)
    parent_name = normalize_name(layer.parent.name) if layer.parent is not None else ""
    grandparent_name = (
        normalize_name(layer.parent.parent.name)
        if layer.parent is not None and layer.parent.parent is not None
        else ""
    )
    if source_stem in {"Buttons", "Component"} and parent_name in {"prg", "bar"}:
        sibling_index = next(index for index, child in enumerate(layer.parent) if child is layer)
        if grandparent_name in {"control", "bar"}:
            return "Track" if sibling_index == 0 else "Fill"
    if source_stem == "Component" and parent_name == "point":
        return "Overlay"
    if parent_name == "progress" and normalized == "bar":
        return "Background"
    if parent_name == "progress" and normalized == "prg":
        return "Fill"
    words = {normalize_name(word) for word in split_words(layer.name)}
    width, height = bbox_size(layer)
    reference_path = normalize_name(layer_reference(layer).display_path)
    if normalized in {"popup", "popupbg"} or (
        source_stem.startswith("Popup") and normalized == "login"
    ):
        return "Sliced"
    if parent_name == "textinput" and normalized in {"entered", "error", "required", "typing"}:
        return "Sliced"
    if normalized == "frame" and "loadingbar" in reference_path:
        return "Sliced"
    for key, role in ROLE_NAME_PARTS.items():
        if normalized == key or key in words:
            return role
    if "textfield" in normalized:
        return "Sliced"
    if parent_name.startswith(("button", "btn")) and normalized in {"btn", "button"}:
        # 横向矩形按钮可九宫格拉伸；圆形按钮和接近正方形的关闭按钮保持原图。
        return "Sliced" if height > 0 and width >= height * 2 else "Normal"
    if normalize_name(source_stem) == normalized == "background":
        return "Background"
    return "Simple"


def infer_border(role: str, layer: Any) -> list[int]:
    if role != "Sliced":
        return [0, 0, 0, 0]
    width, height = bbox_size(layer)
    if width < 12 or height < 12:
        return [0, 0, 0, 0]
    horizontal = min(32, max(4, int(round(height * 0.22))), width // 3)
    vertical = min(24, max(4, int(round(height * 0.22))), height // 3)
    return [horizontal, vertical, horizontal, vertical]


def output_name_for(source_stem: str, category: str, layer: Any, role: str) -> str:
    if source_stem == "FunctionIcons":
        original = re.sub(r"^btn_icon_", "", layer.name, flags=re.IGNORECASE)
        return "UI_FunctionIcon_" + pascal_token(original)

    if source_stem == "Buttons" and normalize_name(layer.name) in {"btn", "icon"}:
        parent_name = layer.parent.name if layer.parent is not None else "Button"
        color = re.sub(r"^(Button|Btn)[ _-]*", "", parent_name, flags=re.IGNORECASE)
        reference_path = layer_reference(layer).display_path
        shape = "Circle" if "Common Buttons/" in reference_path else "Rect"
        suffix = "Icon" if normalize_name(layer.name) == "icon" else "Normal"
        return f"UI_Common_Button_{shape}_{pascal_token(color)}_{suffix}"

    reference_path = layer_reference(layer).display_path
    if source_stem == "Buttons" and "ControlBar/Control/" in reference_path:
        return f"UI_Common_Slider_Orange_{pascal_token(role)}"
    if source_stem == "Component" and reference_path.startswith("Stage Progress/Bar/"):
        suffix = "Points" if normalize_name(layer.name) == "point" else pascal_token(role)
        return f"UI_Common_StageProgress_{suffix}"

    tokens = significant_path_tokens(source_stem, layer)
    source_token = pascal_token(source_stem)
    if category == source_token:
        base_tokens = [category]
    else:
        base_tokens = [category, source_token]
    base_tokens.extend(tokens or [pascal_token(layer.name)])

    role_token = pascal_token(role)
    normalized_tokens = {normalize_name(token) for token in base_tokens}
    if role not in {"Simple", "Sliced"} and normalize_name(role_token) not in normalized_tokens:
        base_tokens.append(role_token)
    is_button_base = normalize_name(layer.name) in {"btn", "button"}
    if (role == "Normal" or (role == "Sliced" and is_button_base)) and "normal" not in normalized_tokens:
        base_tokens.append("Normal")

    compact: list[str] = []
    for token in base_tokens:
        if compact and normalize_name(compact[-1]) == normalize_name(token):
            continue
        compact.append(token)
    return "UI_" + "_".join(compact)


def english_words_for_output(output_name: str) -> list[str]:
    tokens: list[str] = []
    for section in output_name.removeprefix("UI_").split("_"):
        tokens.extend(split_words(section))
    return [token.lower() for token in tokens]


def chinese_name_for(output_name: str, category: str) -> str:
    words = english_words_for_output(output_name)[1:]
    translated: list[str] = []
    for word in words:
        if word in TOKEN_CHINESE:
            value = TOKEN_CHINESE[word]
        elif word.isdigit():
            value = f"{int(word):02d}号"
        else:
            value = word[:1].upper() + word[1:]
        if not translated or translated[-1] != value:
            translated.append(value)
    detail = " / ".join(translated) if translated else "未分类组件"
    return f"{CATEGORY_CHINESE[category]}：{detail}"


def unique_name(preferred: str, used: set[str]) -> str:
    candidate = preferred
    suffix = 2
    while candidate.casefold() in used:
        candidate = f"{preferred}_{suffix:02d}"
        suffix += 1
    used.add(candidate.casefold())
    return candidate


def discover_manifest() -> dict[str, Any]:
    psd_paths = sorted(SOURCE_ROOT.glob("*.psd"), key=lambda path: path.name.casefold())
    if len(psd_paths) != EXPECTED_PSD_COUNT:
        raise RuntimeError(
            f"预期 {EXPECTED_PSD_COUNT} 个 PSD，实际找到 {len(psd_paths)} 个：{SOURCE_ROOT}"
        )

    entries: list[dict[str, Any]] = []
    used_names: set[str] = set()

    for psd_path in psd_paths:
        source_stem = psd_path.stem
        if source_stem not in CATEGORY_BY_SOURCE:
            raise RuntimeError(f"没有为 {psd_path.name} 配置分类。")
        category = CATEGORY_BY_SOURCE[source_stem]
        psd = PSDImage.open(psd_path)

        if source_stem == "FunctionIcons":
            candidates = [
                layer
                for layer in psd.descendants()
                if layer.is_group()
                and layer.is_visible()
                and layer.name.lower().startswith("btn_icon_")
                and bbox_size(layer)[0] > 0
            ]
        else:
            candidates = []
            for top_level in psd:
                normalized_top = normalize_name(top_level.name)
                # Buttons/Component 的 Background 只是设计稿展示底板，不是游戏运行时背景。
                if source_stem in {"Buttons", "Component"} and normalized_top == "background":
                    continue
                # 模板标题属于示例游戏品牌；项目会使用自己的标题与 TextMeshPro。
                if source_stem in {"Login", "Title", "TitleStart"} and normalized_top == "title":
                    continue
                candidates.extend(discover_layers(top_level))

        if not candidates:
            raise RuntimeError(f"{psd_path.name} 没有发现可导出的组件。")

        for layer in candidates:
            reference = layer_reference(layer)
            role = infer_role(source_stem, layer)
            preferred_name = output_name_for(source_stem, category, layer, role)
            name = unique_name(preferred_name, used_names)
            exclusions = ["kind:type"]
            if source_stem in RASTER_TEXT_NAMES_BY_SOURCE:
                exclusions.extend(
                    f"name:{value}" for value in sorted(RASTER_TEXT_NAMES_BY_SOURCE[source_stem])
                )
            entries.append(
                {
                    "source_psd": psd_path.name,
                    "layer_path": reference.display_path,
                    "layer_indices": list(reference.indices),
                    "output_name": name,
                    "chinese_name": chinese_name_for(name, category),
                    "category": category,
                    "role": role,
                    "exclude_layers": exclusions,
                    "border": infer_border(role, layer),
                }
            )

    return {
        "version": MANIFEST_VERSION,
        "source_root": "Assets/AllResources/淘宝ui素材",
        "output_root": "Assets/AllResources/淘宝ui素材/RuntimeSprites",
        "text_policy": "Exclude editable text and original template title text",
        "entries": entries,
    }


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def load_manifest() -> dict[str, Any]:
    if not MANIFEST_PATH.exists():
        raise RuntimeError(f"缺少显式导出清单：{MANIFEST_PATH}")
    value = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    if value.get("version") != MANIFEST_VERSION:
        raise RuntimeError("导出清单版本不匹配。")
    return value


def validate_manifest(manifest: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    entries = manifest.get("entries", [])
    source_files = sorted(SOURCE_ROOT.glob("*.psd"), key=lambda path: path.name.casefold())
    source_names = {path.name for path in source_files}
    referenced_sources = {entry.get("source_psd") for entry in entries}

    if len(source_files) != EXPECTED_PSD_COUNT:
        errors.append(f"PSD 数量应为 {EXPECTED_PSD_COUNT}，实际为 {len(source_files)}。")
    for source_name in sorted(source_names - referenced_sources):
        errors.append(f"清单没有覆盖源文件：{source_name}")
    for source_name in sorted(referenced_sources - source_names):
        errors.append(f"清单引用了不存在的源文件：{source_name}")

    names: set[str] = set()
    psd_cache: dict[str, Any] = {}
    for index, entry in enumerate(entries):
        prefix = f"entries[{index}]"
        name = entry.get("output_name", "")
        if not OUTPUT_NAME_PATTERN.fullmatch(name):
            errors.append(f"{prefix} 输出名不符合规范：{name}")
        if name.casefold() in names:
            errors.append(f"{prefix} 输出名大小写重复：{name}")
        names.add(name.casefold())

        category = entry.get("category")
        if category not in CATEGORY_CHINESE:
            errors.append(f"{prefix} 分类无效：{category}")

        border = entry.get("border")
        if not isinstance(border, list) or len(border) != 4 or any(
            not isinstance(value, int) or value < 0 for value in border
        ):
            errors.append(f"{prefix} Border 必须是四个非负整数：{border}")

        source_name = entry.get("source_psd")
        if source_name not in source_names:
            continue
        if source_name not in psd_cache:
            psd_cache[source_name] = PSDImage.open(SOURCE_ROOT / source_name)
        try:
            layer = resolve_layer(psd_cache[source_name], entry.get("layer_indices", []))
            actual_path = layer_reference(layer).display_path
            if actual_path != entry.get("layer_path"):
                errors.append(
                    f"{prefix} 图层索引解析成 {actual_path}，清单记录为 {entry.get('layer_path')}。"
                )
            width, height = bbox_size(layer)
            left, bottom, right, top = border
            if left + right >= width or bottom + top >= height:
                errors.append(f"{prefix} Border 超过图层尺寸 {width}x{height}：{border}")
        except (IndexError, TypeError) as exc:
            errors.append(f"{prefix} 无法解析图层索引：{exc}")
    return errors


def make_layer_filter(source_stem: str, exclusions: Sequence[str]):
    excluded_names = {
        exclusion.removeprefix("name:")
        for exclusion in exclusions
        if exclusion.startswith("name:")
    }

    def layer_filter(layer: Any) -> bool:
        if not layer.is_visible() or layer.kind == "type":
            return False
        normalized = normalize_name(layer.name)
        if normalized in excluded_names:
            return False
        if source_stem in RASTER_TEXT_NAMES_BY_SOURCE and normalized in RASTER_TEXT_NAMES_BY_SOURCE[source_stem]:
            return False
        return True

    return layer_filter


def safe_clean_generated_outputs() -> None:
    expected_output = (SOURCE_ROOT / "RuntimeSprites").resolve()
    if OUTPUT_ROOT.resolve() != expected_output or SOURCE_ROOT.resolve() not in OUTPUT_ROOT.resolve().parents:
        raise RuntimeError(f"拒绝清理非预期目录：{OUTPUT_ROOT}")
    if OUTPUT_ROOT.exists():
        for child in OUTPUT_ROOT.iterdir():
            if child.is_dir():
                shutil.rmtree(child)
            elif child.suffix.lower() in {".png", ".json", ".csv", ".md"}:
                child.unlink()

    expected_preview = (PROJECT_ROOT / "Docs" / "UiAssetCatalog" / "Previews").resolve()
    if PREVIEW_ROOT.resolve() != expected_preview:
        raise RuntimeError(f"拒绝清理非预期预览目录：{PREVIEW_ROOT}")
    if PREVIEW_ROOT.exists():
        for preview in PREVIEW_ROOT.glob("*.png"):
            preview.unlink()


def normalize_runtime_canvas(source_stem: str, image: Image.Image) -> Image.Image:
    """统一要求固定对齐画布的运行时资源，不缩放图标本身。"""
    if source_stem != "FunctionIcons":
        return image

    canvas_width, canvas_height = FUNCTION_ICON_CANVAS_SIZE
    if image.width > canvas_width or image.height > canvas_height:
        raise RuntimeError(
            f"Function Icon 超出统一画布：{image.width}x{image.height} > "
            f"{canvas_width}x{canvas_height}"
        )

    canvas = Image.new("RGBA", FUNCTION_ICON_CANVAS_SIZE, (0, 0, 0, 0))
    offset = ((canvas_width - image.width) // 2, (canvas_height - image.height) // 2)
    canvas.alpha_composite(image, offset)
    return canvas


def export_entries(manifest: dict[str, Any], clean: bool) -> list[dict[str, Any]]:
    if clean:
        safe_clean_generated_outputs()
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)

    rows: list[dict[str, Any]] = []
    exported_by_semantic_hash: dict[tuple[str, str, str, str], dict[str, Any]] = {}
    skipped_output_names: set[str] = set()
    psd_cache: dict[str, Any] = {}
    for entry in manifest["entries"]:
        source_name = entry["source_psd"]
        source_stem = Path(source_name).stem
        if source_name not in psd_cache:
            psd_cache[source_name] = PSDImage.open(SOURCE_ROOT / source_name)
        psd = psd_cache[source_name]
        layer = resolve_layer(psd, entry["layer_indices"])
        image = layer.composite(
            viewport=tuple(layer.bbox),
            layer_filter=make_layer_filter(source_stem, entry["exclude_layers"]),
        )
        if image is None:
            raise RuntimeError(f"导出结果为空：{source_name} -> {entry['layer_path']}")
        image = normalize_runtime_canvas(source_stem, image.convert("RGBA"))
        alpha = image.getchannel("A")
        if alpha.getbbox() is None and getattr(layer, "clipping", False):
            # 剪裁图层单独 composite 时缺少基底会变成全透明；topil 可保留该 Shape
            # 自身的渐变与 Alpha，适合导出 Slider/Progress 的独立 Fill。
            unclipped = layer.topil()
            if unclipped is not None:
                unclipped = normalize_runtime_canvas(source_stem, unclipped.convert("RGBA"))
                if unclipped.getchannel("A").getbbox() is not None:
                    image = unclipped
                    alpha = image.getchannel("A")
        if alpha.getbbox() is None:
            # 部分 PSD 的剪裁蒙版/进度遮罩只有依附父层时才可见，不能作为独立 Sprite。
            # 首次正式导出会把这类条目从显式清单中剔除，后续导出因此保持稳定。
            skipped_output_names.add(entry["output_name"])
            print(
                f"SKIPPED_EMPTY\t{source_name}\t{entry['layer_path']}\t{entry['output_name']}",
                flush=True,
            )
            continue

        sha256 = hashlib.sha256(image.tobytes()).hexdigest()
        terminal_name = normalize_name(entry["layer_path"].split("/")[-1].split("#", 1)[0])
        semantic_key = (sha256, entry["category"], entry["role"], terminal_name)
        existing = exported_by_semantic_hash.get(semantic_key)
        if existing is not None:
            rows.append(
                {
                    **entry,
                    "output_path": existing["output_path"],
                    "resolved_output_name": existing["resolved_output_name"],
                    "is_alias": True,
                    "width": image.width,
                    "height": image.height,
                    "sha256": sha256,
                }
            )
            print(
                f"ALIASED\t{entry['output_name']}\t{existing['resolved_output_name']}",
                flush=True,
            )
            continue

        category_dir = OUTPUT_ROOT / entry["category"]
        category_dir.mkdir(parents=True, exist_ok=True)
        output_path = category_dir / f"{entry['output_name']}.png"
        image.save(output_path, format="PNG", optimize=True)
        relative_output = output_path.relative_to(PROJECT_ROOT).as_posix()
        row = {
            **entry,
            "output_path": relative_output,
            "resolved_output_name": entry["output_name"],
            "is_alias": False,
            "width": image.width,
            "height": image.height,
            "sha256": sha256,
        }
        rows.append(row)
        exported_by_semantic_hash[semantic_key] = row
        print(f"EXPORTED\t{relative_output}\t{image.width}x{image.height}", flush=True)

    if skipped_output_names:
        manifest["entries"] = [
            entry
            for entry in manifest["entries"]
            if entry["output_name"] not in skipped_output_names
        ]
        write_json(MANIFEST_PATH, manifest)
        print(
            f"MANIFEST_PRUNED\tempty_entries={len(skipped_output_names)}\tremaining={len(manifest['entries'])}",
            flush=True,
        )
    return rows


def font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = [
        Path("C:/Windows/Fonts/msyh.ttc"),
        Path("C:/Windows/Fonts/simhei.ttf"),
        Path("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size=size)
    return ImageFont.load_default()


def build_contact_sheets(rows: Sequence[dict[str, Any]]) -> dict[str, list[str]]:
    PREVIEW_ROOT.mkdir(parents=True, exist_ok=True)
    by_category: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        if row.get("is_alias"):
            continue
        by_category[row["category"]].append(row)

    generated: dict[str, list[str]] = defaultdict(list)
    cell_width, cell_height = 340, 250
    columns, rows_per_page = 4, 5
    page_size = columns * rows_per_page
    title_font = font(22)
    label_font = font(15)
    chinese_font = font(14)

    for category, category_rows in sorted(by_category.items()):
        category_rows.sort(key=lambda value: value["output_name"].casefold())
        for page_index in range(0, len(category_rows), page_size):
            page_rows = category_rows[page_index : page_index + page_size]
            sheet = Image.new(
                "RGB", (cell_width * columns, 60 + cell_height * rows_per_page), (27, 24, 36)
            )
            draw = ImageDraw.Draw(sheet)
            page_number = page_index // page_size + 1
            draw.text(
                (24, 16),
                f"{category} / {CATEGORY_CHINESE[category]} / Page {page_number}",
                fill=(245, 225, 180),
                font=title_font,
            )
            for slot, row in enumerate(page_rows):
                column = slot % columns
                line = slot // columns
                x = column * cell_width
                y = 60 + line * cell_height
                draw.rectangle(
                    (x + 8, y + 8, x + cell_width - 8, y + cell_height - 8),
                    fill=(44, 39, 56),
                    outline=(91, 79, 112),
                )
                asset = Image.open(PROJECT_ROOT / row["output_path"]).convert("RGBA")
                asset.thumbnail((cell_width - 40, 150), Image.Resampling.LANCZOS)
                checker = Image.new("RGBA", asset.size, (84, 80, 92, 255))
                checker.alpha_composite(asset)
                image_x = x + (cell_width - asset.width) // 2
                image_y = y + 18 + (150 - asset.height) // 2
                sheet.paste(checker.convert("RGB"), (image_x, image_y))
                draw.text(
                    (x + 16, y + 176),
                    row["output_name"],
                    fill=(240, 240, 245),
                    font=label_font,
                )
                draw.text(
                    (x + 16, y + 204),
                    row["chinese_name"],
                    fill=(186, 210, 239),
                    font=chinese_font,
                )
            preview_name = f"{category}_{page_number:02d}.png"
            preview_path = PREVIEW_ROOT / preview_name
            sheet.save(preview_path, format="PNG", optimize=True)
            generated[category].append(preview_path.relative_to(PROJECT_ROOT / "Docs").as_posix())
    return generated


def write_catalog(rows: Sequence[dict[str, Any]], previews: dict[str, list[str]]) -> None:
    CATALOG_CSV_PATH.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = [
        "output_name",
        "chinese_name",
        "category",
        "role",
        "source_psd",
        "layer_path",
        "output_path",
        "resolved_output_name",
        "is_alias",
        "width",
        "height",
        "border",
        "sha256",
    ]
    with CATALOG_CSV_PATH.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for row in sorted(rows, key=lambda value: value["output_name"].casefold()):
            value = dict(row)
            value["border"] = ",".join(str(number) for number in row["border"])
            writer.writerow(value)

    lines = [
        "# 淘宝 UI 运行时素材目录",
        "",
        "> 本目录由 `Tools/UiAssetPipeline/export_ui_assets.py` 根据显式清单生成。",
        "> RuntimeSprites 不包含可编辑英文示例文字；文字请在 Unity 中使用 TextMeshPro。",
        "",
        f"清单共 **{len(rows)}** 项，生成 **{len([row for row in rows if not row.get('is_alias')])}** 个不重复 Sprite，来源为 **{len({row['source_psd'] for row in rows})}** 个 PSD。",
        "",
    ]
    by_category: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        by_category[row["category"]].append(row)
    for category, category_rows in sorted(by_category.items()):
        lines.extend(
            [
                f"## {category} / {CATEGORY_CHINESE[category]}",
                "",
                f"数量：{len(category_rows)}",
                "",
            ]
        )
        for preview in previews.get(category, []):
            lines.append(f"![{category} 预览]({preview})")
        lines.extend(
            [
                "",
                "| Sprite 名称 | 中文用途 | 类型 | 来源 PSD / 图层 |",
                "| --- | --- | --- | --- |",
            ]
        )
        for row in sorted(category_rows, key=lambda value: value["output_name"].casefold()):
            source = f"{row['source_psd']} / {row['layer_path']}".replace("|", "\\|")
            sprite_name = row["output_name"]
            if row.get("is_alias"):
                sprite_name += f" → {row['resolved_output_name']}"
            lines.append(
                f"| `{sprite_name}` | {row['chinese_name']} | {row['role']} | {source} |"
            )
        lines.append("")
    CATALOG_MD_PATH.write_text("\n".join(lines), encoding="utf-8")


def write_import_rules(rows: Sequence[dict[str, Any]]) -> None:
    unique_rows = [row for row in rows if not row.get("is_alias")]
    rules = {
        "version": 1,
        "runtimeSpriteRoot": "Assets/AllResources/淘宝ui素材/RuntimeSprites/",
        "sourceRoot": "Assets/AllResources/淘宝ui素材/",
        "sprites": [
            {
                "path": row["output_path"],
                "border": row["border"],
                "role": row["role"],
            }
            for row in sorted(unique_rows, key=lambda value: value["output_path"].casefold())
        ],
    }
    write_json(IMPORT_RULES_PATH, rules)


def command_discover(force: bool) -> None:
    if MANIFEST_PATH.exists() and not force:
        raise RuntimeError("显式清单已存在；如需重建必须传入 --force。")
    manifest = discover_manifest()
    write_json(MANIFEST_PATH, manifest)
    print(f"DISCOVERED\t{len(manifest['entries'])}\t{MANIFEST_PATH}")


def command_dry_run() -> None:
    manifest = load_manifest()
    errors = validate_manifest(manifest)
    if errors:
        for error in errors:
            print(f"ERROR\t{error}")
        raise RuntimeError(f"清单校验失败，共 {len(errors)} 项。")
    counts = defaultdict(int)
    for entry in manifest["entries"]:
        counts[entry["category"]] += 1
    print(f"DRY_RUN_OK\tentries={len(manifest['entries'])}\tpsd={len(set(entry['source_psd'] for entry in manifest['entries']))}")
    for category, count in sorted(counts.items()):
        print(f"CATEGORY\t{category}\t{count}")


def command_export(clean: bool) -> None:
    manifest = load_manifest()
    errors = validate_manifest(manifest)
    if errors:
        raise RuntimeError("导出前清单校验失败：\n" + "\n".join(errors))
    rows = export_entries(manifest, clean=clean)
    previews = build_contact_sheets(rows)
    write_catalog(rows, previews)
    write_import_rules(rows)
    print(f"EXPORT_OK\tentries={len(rows)}\toutput={OUTPUT_ROOT}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Treasure Hunter 淘宝 UI PSD 导出工具")
    subparsers = parser.add_subparsers(dest="command", required=True)
    discover_parser = subparsers.add_parser("discover", help="从 PSD 发现组件并生成显式清单")
    discover_parser.add_argument("--force", action="store_true", help="覆盖已有清单")
    subparsers.add_parser("dry-run", help="只检查源文件、清单、图层路径和命名")
    export_parser = subparsers.add_parser("export", help="按显式清单导出 PNG 和目录")
    export_parser.add_argument("--clean", action="store_true", help="安全清理旧的生成结果")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        if args.command == "discover":
            command_discover(args.force)
        elif args.command == "dry-run":
            command_dry_run()
        elif args.command == "export":
            command_export(args.clean)
        else:
            raise RuntimeError(f"未知命令：{args.command}")
        return 0
    except Exception as exc:  # 命令行工具需要把完整失败原因交给调用者。
        print(f"FAILED\t{type(exc).__name__}\t{exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
