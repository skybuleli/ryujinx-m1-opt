import re
import sys
import os
from datetime import datetime

def strip_ansi(text):
    """去除终端的颜色和控制字符"""
    ansi_escape = re.compile(r'\x1B(?:[@-Z\\-_]|[\[0-?]*[ -/]*[@-~])')
    return ansi_escape.sub('', text)

def format_log_to_markdown(raw_log_path, output_md_path):
    if not os.path.exists(raw_log_path):
        print(f"Error: Log file {raw_log_path} not found.")
        return

    with open(raw_log_path, 'r', encoding='utf-8', errors='ignore') as f:
        content = f.read()

    # 清洗 ANSI 字符
    clean_content = strip_ansi(content)
    
    # 分割对话 (这里假设提示符包含某些特征，需要根据实际情况微调)
    # 简单的按行处理，后期可以加入更复杂的 AI 识别逻辑
    lines = clean_content.splitlines()
    
    md_output = []
    current_date = datetime.now().strftime('%Y-%m-%d %H:%M')
    
    md_output.append(f"# Gemini CLI Session - {current_date}\n")
    md_output.append("> Auto-generated session log\n")
    md_output.append("---\n")

    in_code_block = False
    
    for line in lines:
        line = line.strip()
        if not line:
            continue

        # 尝试识别 Prompt (根据您的实际终端提示符修改，例如 '➜' 或用户名)
        # 这里做一个简单的假设，您可以根据实际情况调整
        if "liliang" in line and ("ryujinx" in line or "~" in line): 
            md_output.append(f"\n### 👤 User ({line})\n")
        elif "Gemini" in line or "Thinking" in line:
             md_output.append(f"\n### 🤖 Gemini\n")
        else:
            # 处理代码块逻辑
            if line.startswith("```"):
                in_code_block = not in_code_block
                md_output.append(line)
            else:
                if in_code_block:
                    md_output.append(line)
                else:
                    # 普通文本，加引用样式或者列表样式
                    md_output.append(f"{line}<br>")

    with open(output_md_path, 'w', encoding='utf-8') as f:
        f.write("\n".join(md_output))
    
    print(f"✅ Session saved to: {output_md_path}")

if __name__ == "__main__":
    # 使用方法: python3 log_to_md.py raw_session.log output.md
    if len(sys.argv) < 3:
        print("Usage: python3 log_to_md.py <input_raw_log> <output_md_file>")
    else:
        format_log_to_markdown(sys.argv[1], sys.argv[2])
