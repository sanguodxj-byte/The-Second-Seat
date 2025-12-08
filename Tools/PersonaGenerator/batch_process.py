# -*- coding: utf-8 -*-
"""
批量处理图片生成 RimWorld 角色定义
Batch process images to generate RimWorld character definitions

使用方法 / Usage:
    python batch_process.py

文件夹设置 / Folder Setup:
    - 输入文件夹: ./input_images (放置 .png 图片)
    - 输出文件夹: ./output_mods (生成 XML 文件)
"""

import os
import sys
from rimworld_persona_generator import RimWorldPersonaGenerator

def main():
    """主执行函数"""
    print("=" * 70)
    print("RimWorld Persona Generator - 批量处理模式")
    print("RimWorld Persona Generator - Batch Processing Mode")
    print("=" * 70)
    print()
    
    # 设置输入输出文件夹
    script_dir = os.path.dirname(os.path.abspath(__file__))
    input_folder = os.path.join(script_dir, "input_images")
    output_folder = os.path.join(script_dir, "output_mods")
    
    print(f"?? 输入文件夹 / Input Folder: {input_folder}")
    print(f"?? 输出文件夹 / Output Folder: {output_folder}")
    print()
    
    # 确保输入文件夹存在
    if not os.path.exists(input_folder):
        print(f"??  输入文件夹不存在，正在创建... / Creating input folder...")
        os.makedirs(input_folder)
        print(f"? 已创建文件夹: {input_folder}")
        print(f"   请将 .png 图片放入此文件夹后重新运行脚本")
        print(f"   Please place .png images in this folder and run again")
        return
    
    # 创建生成器实例
    print("?? 初始化生成器... / Initializing generator...")
    rules_path = os.path.join(script_dir, "rules.json")
    generator = RimWorldPersonaGenerator(rules_path if os.path.exists(rules_path) else None)
    print("? 生成器初始化完成")
    print()
    
    # 执行批量处理
    print("?? 开始批量处理... / Starting batch processing...")
    print()
    
    try:
        result = generator.process_image_folder(input_folder, output_folder)
        
        # 打印最终总结
        print()
        print("=" * 70)
        print("?? 处理完成总结 / Processing Summary")
        print("=" * 70)
        print(f"? 成功处理 / Processed: {result['processed']} 个文件")
        print(f"? 处理失败 / Failed: {result['failed']} 个文件")
        
        if result['failed_files']:
            print()
            print("? 失败文件列表 / Failed files:")
            for filename in result['failed_files']:
                print(f"   - {filename}")
        
        print()
        print(f"?? 输出文件位置 / Output files location:")
        print(f"   {output_folder}")
        print("=" * 70)
        
        # 返回状态码
        if result['failed'] > 0:
            sys.exit(1)  # 部分失败
        else:
            sys.exit(0)  # 全部成功
            
    except Exception as e:
        print()
        print("=" * 70)
        print(f"? 致命错误 / Fatal Error")
        print("=" * 70)
        print(f"错误信息: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(2)


if __name__ == "__main__":
    main()
