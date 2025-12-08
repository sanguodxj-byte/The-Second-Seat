# -*- coding: utf-8 -*-
"""
快速测试批量处理功能
Quick test for batch processing functionality
"""

import os
import sys
from PIL import Image, ImageDraw, ImageFont

def create_test_images(output_folder: str = "./input_images", count: int = 3):
    """
    创建测试图片
    Create test images
    
    Args:
        output_folder: 输出文件夹路径
        count: 要创建的图片数量
    """
    os.makedirs(output_folder, exist_ok=True)
    
    test_characters = [
        ("TestCharacter1", (255, 100, 100)),  # 红色
        ("TestCharacter2", (100, 255, 100)),  # 绿色
        ("TestCharacter3", (100, 100, 255)),  # 蓝色
    ]
    
    print("?? 创建测试图片... / Creating test images...")
    
    for i, (name, color) in enumerate(test_characters[:count]):
        # 创建 512x512 的图片
        img = Image.new('RGB', (512, 512), color=color)
        draw = ImageDraw.Draw(img)
        
        # 绘制文字（角色名）
        try:
            font = ImageFont.truetype("arial.ttf", 40)
        except:
            font = ImageFont.load_default()
        
        # 计算文字位置（居中）
        text = name
        # 使用 textbbox 替代已弃用的 textsize
        bbox = draw.textbbox((0, 0), text, font=font)
        text_width = bbox[2] - bbox[0]
        text_height = bbox[3] - bbox[1]
        position = ((512 - text_width) // 2, (512 - text_height) // 2)
        
        # 绘制文字
        draw.text(position, text, fill=(255, 255, 255), font=font)
        
        # 保存图片
        filepath = os.path.join(output_folder, f"{name}.png")
        img.save(filepath)
        print(f"  ? 已创建: {filepath}")
    
    print(f"? 成功创建 {count} 个测试图片")


def test_batch_processing():
    """测试批量处理功能"""
    print("=" * 70)
    print("?? 批量处理功能测试")
    print("=" * 70)
    print()
    
    script_dir = os.path.dirname(os.path.abspath(__file__))
    input_folder = os.path.join(script_dir, "input_images")
    output_folder = os.path.join(script_dir, "output_mods")
    
    # 步骤 1: 创建测试图片
    print("?? 步骤 1: 创建测试图片")
    print("-" * 70)
    create_test_images(input_folder, count=3)
    print()
    
    # 步骤 2: 运行批量处理
    print("?? 步骤 2: 运行批量处理")
    print("-" * 70)
    
    try:
        from rimworld_persona_generator import RimWorldPersonaGenerator
        
        generator = RimWorldPersonaGenerator()
        result = generator.process_image_folder(input_folder, output_folder)
        
        print()
        print("-" * 70)
        print("?? 处理结果:")
        print(f"  ? 成功: {result['processed']} 个文件")
        print(f"  ? 失败: {result['failed']} 个文件")
        
        if result['failed_files']:
            print(f"  失败文件: {', '.join(result['failed_files'])}")
        
        print()
        
        # 步骤 3: 验证输出
        print("?? 步骤 3: 验证输出文件")
        print("-" * 70)
        
        if os.path.exists(output_folder):
            xml_files = [f for f in os.listdir(output_folder) if f.endswith('.xml')]
            print(f"  生成的 XML 文件数量: {len(xml_files)}")
            
            for xml_file in xml_files:
                filepath = os.path.join(output_folder, xml_file)
                file_size = os.path.getsize(filepath)
                print(f"    - {xml_file} ({file_size} bytes)")
            
            # 显示第一个 XML 文件的内容
            if xml_files:
                print()
                print("?? 示例 XML 内容 (第一个文件):")
                print("-" * 70)
                with open(os.path.join(output_folder, xml_files[0]), 'r', encoding='utf-8') as f:
                    content = f.read()
                    # 只显示前 50 行
                    lines = content.split('\n')[:50]
                    print('\n'.join(lines))
                    if len(content.split('\n')) > 50:
                        print("...")
                        print("(内容已截断)")
        
        print()
        print("=" * 70)
        print("? 测试完成！")
        print("=" * 70)
        print()
        print(f"?? 输入文件夹: {input_folder}")
        print(f"?? 输出文件夹: {output_folder}")
        
        return result['processed'] > 0
        
    except Exception as e:
        print()
        print("=" * 70)
        print("? 测试失败")
        print("=" * 70)
        print(f"错误信息: {e}")
        import traceback
        traceback.print_exc()
        return False


if __name__ == "__main__":
    try:
        # 检查是否安装了 Pillow
        try:
            import PIL
        except ImportError:
            print("??  警告: 未安装 Pillow 库，无法创建测试图片")
            print("   请运行: pip install Pillow")
            print()
            print("   将使用已有的图片进行测试...")
            print()
        
        success = test_batch_processing()
        sys.exit(0 if success else 1)
        
    except KeyboardInterrupt:
        print("\n\n??  用户中断")
        sys.exit(130)
