using System;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

public static unsafe class MainApp
{
    private static IWindow? _window;
    private static GL? _gl;
    private static uint _vertexArrayObject;
    private static uint _vertexBufferObject;
    private static uint _elementBufferObject;
    private static uint _shaderProgram;
    private static System.Collections.Generic.List<float> _vertices = null!;
    private static System.Collections.Generic.List<uint> _indices = null!;
    private static ImGuiController _imGuiController = null!;

    private static float x1 = default;
    private static float y1 = default;
    private static float x2 = default;
    private static float y2 = default;
    private static float x3 = default;
    private static float y3 = default;

    private static bool imgui_do_input = false;
    private static readonly byte[] loadFilePathBuffer = new byte[256];
    private static readonly byte[] changeDirBuffer = new byte[256];

    public static void Run()
    {
        _vertices = new System.Collections.Generic.List<float>();
        _indices = new System.Collections.Generic.List<uint>();

        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "2D Triangle Editor";
        options.API = new GraphicsAPI(
            ContextAPI.OpenGL,
            ContextProfile.Core,
            ContextFlags.Default,
            new APIVersion(3, 3));

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Closing += OnClosing;
        _window.Run();
    }

    public static string ReadToken(StreamReader reader)
    {
        var sb = new System.Text.StringBuilder();
        int nextChar;

        while ((nextChar = reader.Read()) != -1 && char.IsWhiteSpace((char)nextChar))
        {
        }

        while (nextChar != -1 && !char.IsWhiteSpace((char)nextChar))
        {
            sb.Append((char)nextChar);
            nextChar = reader.Read();
        }

        return sb.ToString();
    }

    private static void OnLoad()
    {
        _gl = _window!.CreateOpenGL();

        _vertexArrayObject = _gl.GenVertexArray();
        _vertexBufferObject = _gl.GenBuffer();
        _elementBufferObject = _gl.GenBuffer();

        UpdateBufObjectsWithNewData();

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        _shaderProgram = CreateShaderProgram(_gl);

        IInputContext inputContext = _window!.CreateInput();
        _imGuiController = new ImGuiController(_gl, _window, inputContext);
        foreach (var keyboard in inputContext.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
        }
    }

    private static void OnKeyDown(IKeyboard kbd, Key key, int keyCode)
    {
        // key callback function
    }

    private static void OnRender(double delta)
    {
        _imGuiController.Update((float)delta);

        ImGui.Begin("Geometry Editor");
        RenderTriangleControls();
        ImGui.Separator();
        RenderFileControls();
        ImGui.Separator();
        RenderDirectoryControls();
        ImGui.End();

        _gl!.ClearColor(0.1f, 0.12f, 0.16f, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        _gl.UseProgram(_shaderProgram);
        _gl.BindVertexArray(_vertexArrayObject);
        _gl.DrawElements(PrimitiveType.Triangles, (uint)_indices.Count, DrawElementsType.UnsignedInt, null);
        _imGuiController.Render();
    }

    private static void RenderTriangleControls()
    {
        ImGui.Text("Triangle");

        if (ImGui.Button("Add Triangle"))
        {
            imgui_do_input = true;
        }

        if (!imgui_do_input)
        {
            return;
        }

        ImGui.InputFloat("x1", ref x1);
        ImGui.InputFloat("y1", ref y1);
        ImGui.InputFloat("x2", ref x2);
        ImGui.InputFloat("y2", ref y2);
        ImGui.InputFloat("x3", ref x3);
        ImGui.InputFloat("y3", ref y3);

        if (ImGui.Button("Enter Triangle"))
        {
            float[] newFloatArr =
            {
                x1, y1, 0.0f,
                x2, y2, 0.0f,
                x3, y3, 0.0f
            };

            uint[] newIndexArr =
            {
                (uint)_indices.Count, (uint)_indices.Count + 1, (uint)_indices.Count + 2
            };

            UpdateVerticesAndIndicesWithArr(newFloatArr, newIndexArr);
            UpdateBufObjectsWithNewData();

            Console.WriteLine("vertices:");
            foreach (float vertex in _vertices)
            {
                Console.WriteLine($"\t{vertex}");
            }

            imgui_do_input = false;
        }
    }

    private static void RenderFileControls()
    {
        ImGui.Text("Load 2D Shape");
        ImGui.InputText("File Path", loadFilePathBuffer, (uint)loadFilePathBuffer.Length);

        if (ImGui.Button("Load .bfld"))
        {
            string filePath = GetBufferString(loadFilePathBuffer);
            Console.WriteLine($"Response: {filePath}");
            var fdat = GetDataFromFile(filePath);
            System.Console.WriteLine("[DEBUG] adding vertices and index data...");
            UpdateVerticesAndIndices(fdat.Item1, fdat.Item2);
            UpdateBufObjectsWithNewData();
        }
    }

    private static void RenderDirectoryControls()
    {
        ImGui.Text("Working Directory");
        ImGui.Text($"Current: {Environment.CurrentDirectory}");
        ImGui.InputText("Directory Path", changeDirBuffer, (uint)changeDirBuffer.Length);

        if (ImGui.Button("Set Working Directory"))
        {
            string newDirectory = GetBufferString(changeDirBuffer);
            Environment.CurrentDirectory = newDirectory;
            Console.WriteLine($"[DEBUG] cwd set to {Environment.CurrentDirectory}");
        }
    }

    private static string GetBufferString(byte[] buffer)
    {
        int length = Array.IndexOf(buffer, (byte)0);
        if (length < 0)
        {
            length = buffer.Length;
        }

        return System.Text.Encoding.UTF8.GetString(buffer, 0, length);
    }

    private static System.Tuple<System.Collections.Generic.List<float>, System.Collections.Generic.List<uint>> GetDataFromFile(string path)
    {
        var emptyFloatList = new System.Collections.Generic.List<float>();
        var emptyUintList = new System.Collections.Generic.List<uint>();
        var ret = new Tuple<System.Collections.Generic.List<float>, System.Collections.Generic.List<uint>>(emptyFloatList, emptyUintList);

        using FileStream fstrm = new FileStream(path, FileMode.Open, FileAccess.Read);
        using (StreamReader strmRdr = new StreamReader(fstrm))
        {
            if (ReadToken(strmRdr) == "VAO")
            {
                if (ReadToken(strmRdr) == "XYZ")
                {
                    string intstr_num_vertices = ReadToken(strmRdr);
                    if (!int.TryParse(intstr_num_vertices, out int vertices_len))
                    {
                        System.Console.WriteLine("error");
                    }
                    else
                    {
                        System.Console.WriteLine($"num of vertices in file: {vertices_len}");
                        for (uint i = 0; i < vertices_len; i++)
                        {
                            string floatstr = ReadToken(strmRdr);
                            if (!float.TryParse(floatstr, out float this_coord))
                            {
                                System.Console.WriteLine("error");
                                break;
                            }
                            
                            ret.Item1.Add(this_coord);
                            System.Console.WriteLine($"coord: {this_coord}");
                        }

                        if (ReadToken(strmRdr) == "EBO")
                        {
                            System.Console.WriteLine("found ebo token");
                            string intstr_num_indices = ReadToken(strmRdr);
                            if (!int.TryParse(intstr_num_indices, out int indices_len))
                            {
                                System.Console.Error.WriteLine("error");
                            }

                            for (uint i = 0; i < indices_len; i++)
                            {
                                string uintstr_index = ReadToken(strmRdr);
                                if (!uint.TryParse(uintstr_index, out uint uint_index))
                                {
                                    System.Console.Error.WriteLine("error");
                                    break;
                                }
                                ret.Item2.Add(uint_index);
                                System.Console.WriteLine($"[DEBUG] new index `{uint_index}`");
                            }
                        }
                    }
                }
            }
        }

        fstrm.Close();
        
        System.Console.WriteLine($"debug for GetDataFromFile: return lens:\n{ret.Item1.Count}, {ret.Item2.Count}");

        return ret;
    }

    private static void UpdateVerticesAndIndices(System.Collections.Generic.List<float> newVertices, System.Collections.Generic.List<uint> newIndices)
    {
        foreach (float newVertex in newVertices)
        {
            _vertices.Add(newVertex);
        }

        foreach (uint newIndex in newIndices)
        {
            _indices.Add(newIndex);
        }
    }

    private static void UpdateVerticesAndIndicesWithArr(float[] newVertices, uint[] newIndices)
    {
        foreach (float newVertex in newVertices)
        {
            _vertices.Add(newVertex);
        }

        foreach (uint newIndex in newIndices)
        {
            _indices.Add(newIndex);
        }
    }

    private static void UpdateBufObjectsWithNewData()
    {
        _gl!.BindVertexArray(_vertexArrayObject);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBufferObject);
        fixed (float* verticesPtr = CollectionsMarshal.AsSpan(_vertices))
        {
            _gl.BufferData(
                BufferTargetARB.ArrayBuffer,
                (nuint)(_vertices.Count * sizeof(float)),
                verticesPtr,
                BufferUsageARB.StaticDraw);
        }

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _elementBufferObject);
        fixed (uint* indicesPtr = CollectionsMarshal.AsSpan(_indices))
        {
            _gl.BufferData(
                BufferTargetARB.ElementArrayBuffer,
                (nuint)(_indices.Count * sizeof(uint)),
                indicesPtr,
                BufferUsageARB.StaticDraw);
        }

        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);
    }

    private static void OnFramebufferResize(Vector2D<int> size)
    {
        _gl!.Viewport(size);
    }

    private static void OnClosing()
    {
        if (_gl is null)
        {
            return;
        }

        _gl.DeleteBuffer(_vertexBufferObject);
        _gl.DeleteBuffer(_elementBufferObject);
        _gl.DeleteVertexArray(_vertexArrayObject);
        _gl.DeleteProgram(_shaderProgram);
    }

    private static uint CreateShaderProgram(GL gl)
    {
        const string vertexShaderSource = """
            #version 330 core
            layout (location = 0) in vec3 aPosition;

            void main()
            {
                gl_Position = vec4(aPosition, 1.0);
            }
            """;

        const string fragmentShaderSource = """
            #version 330 core
            out vec4 FragColor;

            void main()
            {
                FragColor = vec4(0.95, 0.45, 0.20, 1.0);
            }
            """;

        uint vertexShader = CompileShader(gl, ShaderType.VertexShader, vertexShaderSource);
        uint fragmentShader = CompileShader(gl, ShaderType.FragmentShader, fragmentShaderSource);

        uint shaderProgram = gl.CreateProgram();
        gl.AttachShader(shaderProgram, vertexShader);
        gl.AttachShader(shaderProgram, fragmentShader);
        gl.LinkProgram(shaderProgram);
        gl.GetProgram(shaderProgram, ProgramPropertyARB.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
        {
            throw new InvalidOperationException($"Program link failed: {gl.GetProgramInfoLog(shaderProgram)}");
        }

        gl.DetachShader(shaderProgram, vertexShader);
        gl.DetachShader(shaderProgram, fragmentShader);
        gl.DeleteShader(vertexShader);
        gl.DeleteShader(fragmentShader);

        return shaderProgram;
    }

    private static uint CompileShader(GL gl, ShaderType shaderType, string source)
    {
        uint shader = gl.CreateShader(shaderType);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compileStatus);
        if (compileStatus == 0)
        {
            throw new InvalidOperationException($"{shaderType} compilation failed: {gl.GetShaderInfoLog(shader)}");
        }

        return shader;
    }
}
