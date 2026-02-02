using Shared.Models;
using Shared.Interfaces;
using Shared.Enums;
using Shared.Operations;
using Shared.Services;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Client.Graphics
{
    public class Shader : IDisposable
    {
        private readonly GL _gl;
        private readonly uint _handle;
        private readonly Dictionary<string, int> _uniformLocations = new();

        public Shader(GL gl, string vertexSource, string fragmentSource)
        {
            _gl = gl;

            uint vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
            uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

            _handle = _gl.CreateProgram();
            _gl.AttachShader(_handle, vertexShader);
            _gl.AttachShader(_handle, fragmentShader);
            _gl.LinkProgram(_handle);
            _gl.GetProgram(_handle, GLEnum.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = _gl.GetProgramInfoLog(_handle);
                throw new Exception($"Error linking shader program: {infoLog}");
            }

            _gl.DetachShader(_handle, vertexShader);
            _gl.DetachShader(_handle, fragmentShader);
            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);
        }

        public void Use()
        {
            _gl.UseProgram(_handle);
        }

        public void SetUniform(string name, int value)
        {
            int location = GetUniformLocation(name);
            _gl.Uniform1(location, value);
        }

        public void SetUniform(string name, float value)
        {
            int location = GetUniformLocation(name);
            _gl.Uniform1(location, value);
        }

        public unsafe void SetUniform(string name, Matrix4x4 value)
        {
            int location = GetUniformLocation(name);
            _gl.UniformMatrix4(location, 1, false, (float*)&value);
        }

        public void SetUniform(string name, Vector3 value)
        {
            int location = GetUniformLocation(name);
            _gl.Uniform3(location, value.X, value.Y, value.Z);
        }

        private int GetUniformLocation(string name)
        {
            if (_uniformLocations.TryGetValue(name, out int location))
            {
                return location;
            }

            location = _gl.GetUniformLocation(_handle, name);
            _uniformLocations[name] = location;
            return location;
        }

        private uint CompileShader(ShaderType type, string source)
        {
            uint shader = _gl.CreateShader(type);
            _gl.ShaderSource(shader, source);
            _gl.CompileShader(shader);
            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = _gl.GetShaderInfoLog(shader);
                throw new Exception($"Error compiling shader of type {type}: {infoLog}");
            }
            return shader;
        }

        public void Dispose()
        {
            _gl.DeleteProgram(_handle);
        }
    }
}
