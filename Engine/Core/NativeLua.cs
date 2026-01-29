using System;
using System.Runtime.InteropServices;

namespace Core
{
    public static class NativeLua
    {
        public delegate void LuaHook(IntPtr L, IntPtr ar);

        [DllImport("lua54", CallingConvention = CallingConvention.Cdecl)]
        public static extern int lua_sethook(IntPtr L, LuaHook f, int mask, int count);

        [DllImport("lua54", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr lua_touserdata(IntPtr L, int idx);

        [DllImport("lua54", CallingConvention = CallingConvention.Cdecl)]
        public static extern void lua_error(IntPtr L);

        [DllImport("lua54", CallingConvention = CallingConvention.Cdecl)]
        public static extern int luaL_error(IntPtr L, string message);
    }
}
