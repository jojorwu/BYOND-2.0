using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.OpenGL;

namespace Client.Graphics
{
    public static class MeshFactory
    {
        public static Mesh CreateCube(GL gl)
        {
            MeshVertex[] vertices = new[]
            {
                // Front face
                new MeshVertex(new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(0, 0, 1), new Vector2(0, 0)),
                new MeshVertex(new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(0, 0, 1), new Vector2(1, 0)),
                new MeshVertex(new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(0, 0, 1), new Vector2(1, 1)),
                new MeshVertex(new Vector3(-0.5f,  0.5f,  0.5f), new Vector3(0, 0, 1), new Vector2(0, 1)),

                // Back face
                new MeshVertex(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0, 0, -1), new Vector2(1, 0)),
                new MeshVertex(new Vector3(-0.5f,  0.5f, -0.5f), new Vector3(0, 0, -1), new Vector2(1, 1)),
                new MeshVertex(new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(0, 0, -1), new Vector2(0, 1)),
                new MeshVertex(new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(0, 0, -1), new Vector2(0, 0)),

                // Top face
                new MeshVertex(new Vector3(-0.5f,  0.5f, -0.5f), new Vector3(0, 1, 0), new Vector2(0, 1)),
                new MeshVertex(new Vector3(-0.5f,  0.5f,  0.5f), new Vector3(0, 1, 0), new Vector2(0, 0)),
                new MeshVertex(new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(0, 1, 0), new Vector2(1, 0)),
                new MeshVertex(new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(0, 1, 0), new Vector2(1, 1)),

                // Bottom face
                new MeshVertex(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0, -1, 0), new Vector2(0, 0)),
                new MeshVertex(new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(0, -1, 0), new Vector2(1, 0)),
                new MeshVertex(new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(0, -1, 0), new Vector2(1, 1)),
                new MeshVertex(new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(0, -1, 0), new Vector2(0, 1)),

                // Right face
                new MeshVertex(new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(1, 0, 0), new Vector2(1, 0)),
                new MeshVertex(new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(1, 0, 0), new Vector2(1, 1)),
                new MeshVertex(new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(1, 0, 0), new Vector2(0, 1)),
                new MeshVertex(new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(1, 0, 0), new Vector2(0, 0)),

                // Left face
                new MeshVertex(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-1, 0, 0), new Vector2(0, 0)),
                new MeshVertex(new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(-1, 0, 0), new Vector2(1, 0)),
                new MeshVertex(new Vector3(-0.5f,  0.5f,  0.5f), new Vector3(-1, 0, 0), new Vector2(1, 1)),
                new MeshVertex(new Vector3(-0.5f,  0.5f, -0.5f), new Vector3(-1, 0, 0), new Vector2(0, 1)),
            };

            uint[] indices = new uint[]
            {
                 0,  1,  2,  2,  3,  0,
                 4,  5,  6,  6,  7,  4,
                 8,  9, 10, 10, 11,  8,
                12, 13, 14, 14, 15, 12,
                16, 17, 18, 18, 19, 16,
                20, 21, 22, 22, 23, 20
            };

            return new Mesh(gl, vertices, indices);
        }

        public static Mesh CreatePlane(GL gl)
        {
            MeshVertex[] vertices = new[]
            {
                new MeshVertex(new Vector3(-0.5f, 0, -0.5f), new Vector3(0, 1, 0), new Vector2(0, 0)),
                new MeshVertex(new Vector3( 0.5f, 0, -0.5f), new Vector3(0, 1, 0), new Vector2(1, 0)),
                new MeshVertex(new Vector3( 0.5f, 0,  0.5f), new Vector3(0, 1, 0), new Vector2(1, 1)),
                new MeshVertex(new Vector3(-0.5f, 0,  0.5f), new Vector3(0, 1, 0), new Vector2(0, 1)),
            };

            uint[] indices = new uint[] { 0, 1, 2, 2, 3, 0 };

            return new Mesh(gl, vertices, indices);
        }
    }
}
