import json

file_path = 'assets/locales.json'
with open(file_path, 'r', encoding='utf-8') as f:
    data_root = json.load(f)

locales = data_root['Locales']

# 定义所有需要的语言代码
languages = ["ar_SA", "de_DE", "el_GR", "en_US", "es_ES", "fr_FR", "he_IL", "it_IT", "ja_JP", "ko_KR", "no_NO", "pl_PL", "pt_BR", "ru_RU", "sv_SE", "th_TH", "tr_TR", "uk_UA", "zh_CN", "zh_TW"]

# 定义我们要补全的 Key
new_keys = {
    "SettingsEnableAstcPassthrough": {
        "en": "Enable ASTC Hardware Passthrough",
        "zh": "启用 ASTC 硬件透传",
        "zh_tw": "啟用 ASTC 硬體透傳"
    },
    "SettingsShowMetalHud": {
        "en": "Show Metal Performance HUD",
        "zh": "显示 Metal 性能看板",
        "zh_tw": "顯示 Metal 性能看板"
    },
    "SettingsEnableAstcPassthroughTooltip": {
        "en": "Directly passes ASTC textures to the GPU without CPU decompression. Requires hardware support (e.g., Apple Silicon M1/M2/M3). Reduces RAM/VRAM usage significantly.",
        "zh": "直接将 ASTC 纹理传递给 GPU 而不进行 CPU 解压。需要硬件支持（如 Apple Silicon M1/M2/M3）。可显著减少显存和内存占用。",
        "zh_tw": "直接將 ASTC 材質傳遞給 GPU 而不進行 CPU 解壓。需要硬體支持（如 Apple Silicon M1/M2/M3）。可顯著減少顯存和記憶體佔用。"
    },
    "SettingsShowMetalHudTooltip": {
        "en": "Enables the macOS native Metal performance overlay to monitor GPU usage, memory allocation, and framerates.",
        "zh": "启用 macOS 原生 Metal 性能看板，用于监控 GPU 使用率、显存分配和帧率。",
        "zh_tw": "啟用 macOS 原生 Metal 性能看板，用於監控 GPU 使用率、顯存分配和帧率。"
    }
}

# 过滤并重建 Locales 列表
filtered_locales = [item for item in locales if item['ID'] not in new_keys]

for key_id, translations in new_keys.items():
    full_trans = {}
    for lang in languages:
        if lang == 'zh_CN':
            full_trans[lang] = translations['zh']
        elif lang == 'zh_TW':
            full_trans[lang] = translations['zh_tw']
        else:
            full_trans[lang] = translations['en']
            
    filtered_locales.append({
        "ID": key_id,
        "Translations": full_trans
    })

data_root['Locales'] = filtered_locales

with open(file_path, 'w', encoding='utf-8') as f:
    json.dump(data_root, f, ensure_ascii=False, indent=2)

print("JSON Locale Fix Applied Successfully (Fixed Key structure)")