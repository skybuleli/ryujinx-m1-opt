using NUnit.Framework;
using Ryujinx.Graphics.Gpu;
using Ryujinx.Graphics.Gpu.Image;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Texture.Astc;
using System;

namespace Ryujinx.Tests.Gpu
{
    [TestFixture]
    public class AstcPassthroughTest
    {
        [Test]
        public void TestAstcPassthroughLogic()
        {
            // 验证 GraphicsConfig 是否包含新属性
            Assert.DoesNotThrow(() => {
                bool value = GraphicsConfig.EnableAstcPassthrough;
                GraphicsConfig.EnableAstcPassthrough = true;
            });

            Assert.IsTrue(GraphicsConfig.EnableAstcPassthrough);
            
            // 恢复默认
            GraphicsConfig.EnableAstcPassthrough = false;
        }

        [Test]
        public void TestAstcFormatDetection()
        {
            // 验证 IsAstc 扩展方法是否正常工作
            Assert.IsTrue(Format.Astc4x4Unorm.IsAstc());
            Assert.IsTrue(Format.Astc12x12Srgb.IsAstc());
            Assert.IsFalse(Format.R8G8B8A8Unorm.IsAstc());
        }
    }
}
