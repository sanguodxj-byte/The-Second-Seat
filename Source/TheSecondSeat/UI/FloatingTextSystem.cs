using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace TheSecondSeat.UI
{
    /// <summary>
    /// 管理浮动文字的创建、更新和绘制。
    /// 从 FullBodyPortraitPanel 中分离出来的独立系统。
    /// </summary>
    public class FloatingTextSystem
    {
        private class UIFloatingText
        {
            public string text;
            public Vector2 position;
            public float timer;
            public Color color;
            public float maxLifetime = 2f;

            public UIFloatingText(string text, Vector2 position, Color color)
            {
                this.text = text;
                this.position = position;
                this.color = color;
                this.timer = 0f;
            }

            public bool Update(float deltaTime)
            {
                timer += deltaTime;
                position.y -= 30f * deltaTime;
                return timer < maxLifetime;
            }

            public void Draw()
            {
                float alpha = Mathf.Lerp(1f, 0f, timer / maxLifetime);
                Color drawColor = new Color(color.r, color.g, color.b, alpha);

                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = drawColor;

                Rect textRect = new Rect(position.x - 100f, position.y - 15f, 200f, 30f);
                Widgets.Label(textRect, text);

                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private List<UIFloatingText> floatingTexts = new List<UIFloatingText>();

        /// <summary>
        /// 添加一个新的浮动文字。
        /// </summary>
        public void Add(string text, Vector2 startPosition, Color color)
        {
            floatingTexts.Add(new UIFloatingText(text, startPosition, color));
        }

        /// <summary>
        /// 更新并绘制所有活动的浮动文字。
        /// </summary>
        public void UpdateAndDraw()
        {
            float deltaTime = Time.deltaTime;
            for (int i = floatingTexts.Count - 1; i >= 0; i--)
            {
                var text = floatingTexts[i];
                if (!text.Update(deltaTime))
                {
                    floatingTexts.RemoveAt(i);
                }
                else
                {
                    text.Draw();
                }
            }
        }
    }
}