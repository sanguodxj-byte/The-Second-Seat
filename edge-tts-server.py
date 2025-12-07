from flask import Flask, request, send_file
import edge_tts
import asyncio
import io

app = Flask(__name__)

@app.route('/tts', methods=['POST'])
def tts():
    try:
        data = request.json
        text = data.get('text', '')
        voice = data.get('voice', 'zh-CN-XiaoxiaoNeural')
        rate = data.get('rate', 1.0)
        volume = data.get('volume', 1.0)
        
        if not text:
            return {'error': 'Text is required'}, 400
        
        # 计算速率字符串
        rate_str = f"+{int((rate - 1) * 100)}%" if rate >= 1.0 else f"{int((rate - 1) * 100)}%"
        
        # 生成音频
        audio_data = asyncio.run(generate_audio(text, voice, rate_str, volume))
        
        # 返回音频数据
        return send_file(
            io.BytesIO(audio_data),
            mimetype='audio/wav',
            as_attachment=True,
            download_name='tts.wav'
        )
    except Exception as e:
        return {'error': str(e)}, 500

async def generate_audio(text, voice, rate, volume):
    """生成 TTS 音频"""
    communicate = edge_tts.Communicate(
        text,
        voice,
        rate=rate,
        volume=f"+{int((volume - 1) * 100)}%" if volume >= 1.0 else f"{int((volume - 1) * 100)}%"
    )
    
    audio_data = b""
    async for chunk in communicate.stream():
        if chunk["type"] == "audio":
            audio_data += chunk["data"]
    
    return audio_data

@app.route('/test', methods=['GET'])
def test():
    return {'status': 'ok', 'message': 'Edge TTS server is running'}

if __name__ == '__main__':
    print("=" * 60)
    print(" Edge TTS HTTP 服务器")
    print("=" * 60)
    print(" 监听地址: http://localhost:8000")
    print(" 测试端点: http://localhost:8000/test")
    print(" TTS 端点: http://localhost:8000/tts (POST)")
    print("=" * 60)
    app.run(host='0.0.0.0', port=8000, debug=False)
