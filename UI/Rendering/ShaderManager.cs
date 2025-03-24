using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using DeepBridgeWindowsApp.Utils;

namespace DeepBridgeWindowsApp.UI.Rendering
{
    /// <summary>
    /// Manages OpenGL shaders for the 3D rendering subsystem.
    /// </summary>
    public class ShaderManager : DisposableBase
    {
        #region Shader Source Code

        // Vertex shader for rendering points with colors
        private const string PointVertexShaderSource = @"
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aColor;
        out vec3 vertexColor;
        uniform mat4 model;
        uniform mat4 view;
        uniform mat4 projection;
        void main()
        {
            gl_Position = projection * view * model * vec4(aPosition, 1.0);
            vertexColor = aColor;
        }";

        // Fragment shader for rendering points with colors
        private const string PointFragmentShaderSource = @"
        #version 330 core
        in vec3 vertexColor;
        out vec4 FragColor;
        void main()
        {
            FragColor = vec4(vertexColor, 1.0);
        }";

        // Vertex shader for rendering colored geometry
        private const string ColorVertexShaderSource = @"
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        uniform mat4 model;
        uniform mat4 view;
        uniform mat4 projection;
        void main() {
            gl_Position = projection * view * model * vec4(aPosition, 1.0);
        }";

        // Fragment shader for rendering colored geometry
        private const string ColorFragmentShaderSource = @"
        #version 330 core
        uniform vec3 color;
        out vec4 FragColor;
        void main() {
            FragColor = vec4(color, 0.5);
        }";

        #endregion

        #region Fields

        private readonly Dictionary<string, int> _shaderPrograms = new Dictionary<string, int>();
        private readonly Dictionary<int, List<int>> _attachedShaders = new Dictionary<int, List<int>>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the shader program for rendering points with colors.
        /// </summary>
        public int PointShaderProgram => GetShaderProgram("point");

        /// <summary>
        /// Gets the shader program for rendering colored geometry.
        /// </summary>
        public int ColorShaderProgram => GetShaderProgram("color");

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new ShaderManager instance.
        /// </summary>
        public ShaderManager()
        {
            // Initialize shader programs
            InitializeShaders();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets a shader program by name.
        /// </summary>
        /// <param name="name">The name of the shader program.</param>
        /// <returns>The shader program ID.</returns>
        public int GetShaderProgram(string name)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name), "Shader program name cannot be null or empty");

            if (_shaderPrograms.TryGetValue(name.ToLower(), out int program))
                return program;

            throw new ArgumentException($"Shader program '{name}' not found", nameof(name));
        }

        /// <summary>
        /// Uses a shader program for rendering.
        /// </summary>
        /// <param name="name">The name of the shader program to use.</param>
        public void UseShader(string name)
        {
            ThrowIfDisposed();
            GL.UseProgram(GetShaderProgram(name));
        }

        /// <summary>
        /// Creates a new shader program with custom source code.
        /// </summary>
        /// <param name="name">The name for the new shader program.</param>
        /// <param name="vertexSource">The vertex shader source code.</param>
        /// <param name="fragmentSource">The fragment shader source code.</param>
        /// <returns>The shader program ID.</returns>
        public int CreateShaderProgram(string name, string vertexSource, string fragmentSource)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name), "Shader program name cannot be null or empty");

            if (_shaderPrograms.ContainsKey(name.ToLower()))
                throw new ArgumentException($"Shader program '{name}' already exists", nameof(name));

            int program = CompileShaderProgram(vertexSource, fragmentSource);
            _shaderPrograms[name.ToLower()] = program;
            return program;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initializes the built-in shader programs.
        /// </summary>
        private void InitializeShaders()
        {
            try
            {
                // Create point shader program
                int pointProgram = CompileShaderProgram(PointVertexShaderSource, PointFragmentShaderSource);
                _shaderPrograms["point"] = pointProgram;

                // Create color shader program
                int colorProgram = CompileShaderProgram(ColorVertexShaderSource, ColorFragmentShaderSource);
                _shaderPrograms["color"] = colorProgram;

                Logger.Info("Shader programs initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize shader programs", ex);
                throw;
            }
        }

        /// <summary>
        /// Compiles a shader program from vertex and fragment shader source code.
        /// </summary>
        /// <param name="vertexSource">The vertex shader source code.</param>
        /// <param name="fragmentSource">The fragment shader source code.</param>
        /// <returns>The compiled shader program ID.</returns>
        private int CompileShaderProgram(string vertexSource, string fragmentSource)
        {
            // Compile vertex shader
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexSource);
            GL.CompileShader(vertexShader);

            // Check for compilation errors
            GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int vertexSuccess);
            if (vertexSuccess == 0)
            {
                string infoLog = GL.GetShaderInfoLog(vertexShader);
                GL.DeleteShader(vertexShader);
                throw new Exception($"Vertex shader compilation failed: {infoLog}");
            }

            // Compile fragment shader
            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentSource);
            GL.CompileShader(fragmentShader);

            // Check for compilation errors
            GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out int fragmentSuccess);
            if (fragmentSuccess == 0)
            {
                string infoLog = GL.GetShaderInfoLog(fragmentShader);
                GL.DeleteShader(vertexShader);
                GL.DeleteShader(fragmentShader);
                throw new Exception($"Fragment shader compilation failed: {infoLog}");
            }

            // Create and link shader program
            int program = GL.CreateProgram();
            GL.AttachShader(program, vertexShader);
            GL.AttachShader(program, fragmentShader);
            GL.LinkProgram(program);

            // Check for linking errors
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linkSuccess);
            if (linkSuccess == 0)
            {
                string infoLog = GL.GetProgramInfoLog(program);
                GL.DeleteShader(vertexShader);
                GL.DeleteShader(fragmentShader);
                GL.DeleteProgram(program);
                throw new Exception($"Shader program linking failed: {infoLog}");
            }

            // Track attached shaders for cleanup
            _attachedShaders[program] = new List<int> { vertexShader, fragmentShader };

            return program;
        }

        /// <summary>
        /// Gets the info log for a shader.
        /// </summary>
        /// <param name="shader">The shader ID.</param>
        /// <returns>The shader info log.</returns>
        private string GetShaderInfoLog(int shader)
        {
            GL.GetShader(shader, ShaderParameter.InfoLogLength, out int length);
            if (length > 0)
            {
                return GL.GetShaderInfoLog(shader);
            }
            return string.Empty;
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases unmanaged OpenGL resources.
        /// </summary>
        protected override void DisposeUnmanagedResources()
        {
            try
            {
                // Delete all shader programs and their attached shaders
                foreach (var program in _shaderPrograms.Values)
                {
                    if (_attachedShaders.TryGetValue(program, out var attachedShaders))
                    {
                        // Detach and delete shaders
                        foreach (var shader in attachedShaders)
                        {
                            GL.DetachShader(program, shader);
                            GL.DeleteShader(shader);
                        }
                        _attachedShaders.Remove(program);
                    }

                    // Delete program
                    GL.DeleteProgram(program);
                }

                _shaderPrograms.Clear();
                Logger.Info("Shader programs disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error disposing shader programs: {ex.Message}");
            }
        }

        #endregion
    }
}