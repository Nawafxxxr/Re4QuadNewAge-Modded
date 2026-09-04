using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Re4QuadExtremeEditor.src.Class.Shaders;

namespace NewAgeTheRender
{
    /// <summary>
    /// dynamic world-space geometry renderer used by the CAM file views.
    /// draws raw triangle/line vertex soups (arbitrary trigger polygons,
    /// camera keyframe pyramids) with a minimal position-only shader.
    /// </summary>
    public static class CamZoneRender
    {
        private const string VertSrc = @"#version 330 core
layout(location = 0) in vec3 aPosition;
uniform mat4 view;
uniform mat4 projection;
void main(void)
{
    vec4 newPos = vec4(aPosition, 1.0);
    gl_Position = newPos * view * projection;
}";

        private const string FragSrc = @"#version 330
uniform vec4 mColor;
void main()
{
    gl_FragColor = mColor;
}";

        private static IShader shader = null;
        private static int vaoHandle = 0;
        private static int vboHandle = 0;
        private static int vboCapacityFloats = 0;
        private static bool glInitialized = false;

        private static void EnsureCreated()
        {
            if (shader != null && glInitialized)
            {
                return;
            }
            // (Re)create the shader + VAO/VBO. The cached handles may have been
            // invalidated by a model/GL reload (e.g. Force Reload), so always
            // rebuild from scratch here to avoid drawing through stale native handles.
            shader = new Shader(VertSrc, FragSrc);

            vaoHandle = GL.GenVertexArray();
            vboHandle = GL.GenBuffer();

            GL.BindVertexArray(vaoHandle);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboHandle);
            GL.VertexAttribPointer((int)ViewerBase.AttribLocation.aPosition, 3, VertexAttribPointerType.Float, false, 12, 0);
            GL.EnableVertexAttribArray((int)ViewerBase.AttribLocation.aPosition);
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            glInitialized = true;
        }

        /// <summary>
        /// invalidates the cached GL resources. Call after any operation that
        /// deletes or recreates the GL context/buffers (e.g. Force Reload) so
        /// the next SetupView rebuilds valid handles instead of drawing through
        /// stale native handles (which caused an AccessViolation in DrawArrays).
        /// </summary>
        public static void ResetGLState()
        {
            shader = null;
            vaoHandle = 0;
            vboHandle = 0;
            vboCapacityFloats = 0;
            glInitialized = false;
        }

        /// <summary>must be called once per frame while the context is current</summary>
        public static void SetupView(Matrix4 camMtx, Matrix4 projMatrix)
        {
            EnsureCreated();
            shader.Use();
            shader.SetMatrix4("view", camMtx);
            shader.SetMatrix4("projection", projMatrix);
        }

        private static void Upload(float[] data)
        {
            EnsureCreated();
            GL.BindVertexArray(vaoHandle);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboHandle);
            if (data.Length > vboCapacityFloats)
            {
                vboCapacityFloats = data.Length + (data.Length >> 1) + 256;
                GL.BufferData(BufferTarget.ArrayBuffer, vboCapacityFloats * sizeof(float), data, BufferUsageHint.StreamDraw);
            }
            else
            {
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, data.Length * sizeof(float), data);
            }
        }

        /// <summary>opaque single-pass triangle soup (select-mode picking fills)</summary>
        public static void DrawTrianglesOpaque(float[] triangles, Vector4 color)
        {
            if (triangles == null || triangles.Length < 9)
            {
                return;
            }
            Upload(triangles);
            shader.Use();
            shader.SetVector4("mColor", color);
            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawArrays(PrimitiveType.Triangles, 0, triangles.Length / 3);
            GL.BindVertexArray(0);
        }

        /// <summary>
        /// blended triangle soup for see-through trigger volumes;
        /// caller supplies the alpha inside <paramref name="color"/>
        /// </summary>
        public static void DrawTrianglesTransparent(float[] triangles, Vector4 color)
        {
            if (triangles == null || triangles.Length < 9)
            {
                return;
            }
            Upload(triangles);
            shader.Use();
            shader.SetVector4("mColor", color);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.DepthMask(false);
            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawArrays(PrimitiveType.Triangles, 0, triangles.Length / 3);
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.BindVertexArray(0);
        }

        /// <summary>line soup (prism outlines, aim lines)</summary>
        public static void DrawLines(float[] lineVerts, Vector4 color)
        {
            if (lineVerts == null || lineVerts.Length < 6)
            {
                return;
            }
            // A native draw through a stale/invalid GL resource raises an
            // AccessViolationException (corrupted-state) that cannot be caught by
            // a normal try/catch and otherwise silently kills the whole app.
            // Guard it so a single bad draw is logged and the editor keeps running.
            try
            {
                DrawLinesInner(lineVerts, color);
            }
            catch (Exception ex)
            {
                try { System.IO.File.AppendAllText(@"C:\Temp\RTP_Debug.log", $"{DateTime.Now:HH:mm:ss} DrawLines AV guard ex: {ex.GetType().Name}: {ex.Message}\n"); } catch { }
            }
        }

        [HandleProcessCorruptedStateExceptions]
        private static void DrawLinesInner(float[] lineVerts, Vector4 color)
        {
            Upload(lineVerts);
            shader.Use();
            shader.SetVector4("mColor", color);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawArrays(PrimitiveType.Lines, 0, lineVerts.Length / 3);
            GL.BindVertexArray(0);
        }

        /// <summary>opaque pyramid-style mesh fill plus edge lines</summary>
        public static void DrawMeshWithEdges(float[] triangles, float[] edges, Vector4 color, Vector4 edgeColor)
        {
            if (triangles != null && triangles.Length >= 9)
            {
                Upload(triangles);
                shader.Use();
                shader.SetVector4("mColor", color);
                GL.Disable(EnableCap.CullFace);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                GL.DrawArrays(PrimitiveType.Triangles, 0, triangles.Length / 3);
            }
            if (edges != null && edges.Length >= 6)
            {
                Upload(edges);
                shader.Use();
                shader.SetVector4("mColor", edgeColor);
                GL.DrawArrays(PrimitiveType.Lines, 0, edges.Length / 3);
            }
            GL.BindVertexArray(0);
        }
    }
}
