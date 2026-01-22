import edge_tts
import asyncio

async def test():
    c = edge_tts.Communicate('你好，这是一个测试。', 'zh-CN-XiaoxiaoNeural')
    await c.save('test_py.mp3')
    print('成功生成 test_py.mp3')

asyncio.run(test())
