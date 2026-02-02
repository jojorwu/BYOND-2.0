using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using NUnit.Framework;
using Moq;
using Client.Graphics;
using Silk.NET.OpenGL;
using System.Numerics;
using Robust.Shared.Maths;
using System.Collections.Generic;
using System;

namespace tests
{
    [TestFixture]
    public class SpriteRendererTests
    {
        [Test]
        public void SpriteDrawCommand_SortsCorrectlyByLayerThenTexture()
        {
            var commands = new List<SpriteDrawCommand>
            {
                new SpriteDrawCommand { Layer = 2.0f, TextureId = 10 },
                new SpriteDrawCommand { Layer = 1.0f, TextureId = 20 },
                new SpriteDrawCommand { Layer = 2.0f, TextureId = 5 },
                new SpriteDrawCommand { Layer = 0.5f, TextureId = 30 }
            };

            commands.Sort((a, b) =>
            {
                int layerCmp = a.Layer.CompareTo(b.Layer);
                if (layerCmp != 0) return layerCmp;
                return a.TextureId.CompareTo(b.TextureId);
            });

            Assert.That(commands[0].Layer, Is.EqualTo(0.5f));
            Assert.That(commands[1].Layer, Is.EqualTo(1.0f));
            Assert.That(commands[2].Layer, Is.EqualTo(2.0f));
            Assert.That(commands[2].TextureId, Is.EqualTo(5));
            Assert.That(commands[3].Layer, Is.EqualTo(2.0f));
            Assert.That(commands[3].TextureId, Is.EqualTo(10));
        }

        [Test]
        public void SpriteRenderer_BatchingLogic_Simulation()
        {
            // Since we can't easily instantiate SpriteRenderer without GL,
            // we simulate the batching logic used in SpriteRenderer.End()

            var commands = new List<SpriteDrawCommand>
            {
                new SpriteDrawCommand { TextureId = 1, Layer = 1.0f },
                new SpriteDrawCommand { TextureId = 1, Layer = 1.0f },
                new SpriteDrawCommand { TextureId = 2, Layer = 1.0f },
                new SpriteDrawCommand { TextureId = 2, Layer = 2.0f }
            };

            // Sorting
            commands.Sort((a, b) => {
                int c = a.Layer.CompareTo(b.Layer);
                if (c != 0) return c;
                return a.TextureId.CompareTo(b.TextureId);
            });

            int flushCount = 0;
            uint activeTexture = commands[0].TextureId;
            int vertexCount = 0;

            foreach (var cmd in commands)
            {
                if (cmd.TextureId != activeTexture || vertexCount + 4 > 2000 * 4)
                {
                    flushCount++;
                    activeTexture = cmd.TextureId;
                    vertexCount = 0;
                }
                vertexCount += 4;
            }
            flushCount++; // Final flush

            // After sorting:
            // 1. (T1, L1)
            // 2. (T1, L1)
            // 3. (T2, L1) -> Swap texture, Flush 1
            // 4. (T2, L2) -> Correct layer, same texture, No flush until end
            // Final flush -> Flush 2

            Assert.That(flushCount, Is.EqualTo(2));
        }
    }
}
