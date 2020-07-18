using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using BGFX.Net;
using ImGuiNET;
using SDL2;
using StbImageWriteSharp;

namespace NDS_Toolkit
{
    internal class Program
    {
        public struct Effect
        {
            public Bgfx.ProgramHandle Program;
            public Bgfx.ShaderHandle VertexShader;
            public Bgfx.ShaderHandle FragmentShader;

            public Effect(byte[] vertexData, byte[] fragmentData)
            {
                VertexShader = Bgfx.CreateShader(vertexData);
                FragmentShader = Bgfx.CreateShader(fragmentData);
                Program = Bgfx.CreateProgram(VertexShader, FragmentShader, true);
            }
        }

        static Effect LoadEffect(string vsFile, string fsFile)
        {
            var vsData = File.ReadAllBytes(vsFile);
            var fsData = File.ReadAllBytes(fsFile);
            var effect = new Effect(vsData, fsData);
            return effect;
        }

        static void OrthographicMatrix(out float[] result, float left, float right, float bottom, float top, float near,
            float far, float offset, bool homogeneousNdc, bool isLeftHand = true)
        {
            result = new float[16];
            float aa = 2.0f / (right - left);
            float bb = 2.0f / (top - bottom);
            float cc = (homogeneousNdc ? 2.0f : 1.0f) / (far - near);
            float dd = (left + right) / (left - right);
            float ee = (top + bottom) / (bottom - top);
            float ff = homogeneousNdc
                    ? (near + far) / (near - far)
                    : near / (near - far)
                ;
            result[0] = aa;
            result[5] = bb;
            result[10] = isLeftHand ? cc : -cc;
            result[12] = dd + offset;
            result[13] = ee;
            result[14] = ff;
            result[15] = 1.0f;
        }
        public enum ColorFormat : byte
        {
            A3I5 = 1,           // 8 bits-> 0-4: index; 5-7: alpha
            Colors4 = 2,        // 2 bits for 4 colors
            Colors16 = 3,       // 4 bits for 16 colors
            Colors256 = 4,      // 8 bits for 256 colors
            Texel4X4 = 5,       // 32bits, 2bits per Texel (only in textures)
            A5I3 = 6,           // 8 bits-> 0-2: index; 3-7: alpha
            Direct = 7,         // 16bits, color with BGR555 encoding
            Colors2 = 8,        // 1 bit for 2 colors
            Bgra32 = 9,         // 32 bits -> ABGR
            A4I4 = 10,
            Abgr32 = 11,
        }
        public enum ColorEncoding : byte
        {
            Bgr555 = 1,
            Bgr = 2,
            Rgb = 3
        }
        public enum TileForm
        {
            Lineal,
            Horizontal,
            Vertical
        }

        public enum FileTypes
        {
            NCLR,
            NCGR,
            NCER
        }
        public struct ParsedFile
        {
            public FileTypes type;
            public object Data;
            public string FullPath;
        }
        public struct Ntfs              // Nintedo Tile Format Screen
        {
            public byte NPalette;        // The parameters (two bytes) is PPPP Y X NNNNNNNNNN
            public byte XFlip;
            public byte YFlip;
            public ushort NTile;
        }
        public struct Ntft              // Nintendo Tile Format Tile
        {
            public byte[] Tiles;
            public byte[] NPalette;     // Number of the palette that this tile uses
        }
        public struct NitroHeader
        {
            public char[] Magic;
            public ushort Endianess;            // 0xFFFE -> little endian
            public ushort Constant;
            public uint Sectionsize;
            public ushort Headersize;
            public ushort Sectioncount;
        }
        public struct PlttHeader
        {
            public char[] Magic;
            public uint Length;
            public ColorFormat Depth;
            public ushort Unknown1;
            public uint Unknown2;
            public uint PaletteLength;
            public uint NumColors;
            public uint[][] Palettes;
        }
        public struct PmcpHeader
        {
            public char[] Magic;
            public uint Sectionsize;
            public ushort Palettecount;
            public ushort Unknown0;
            public uint Unknown1;
            public ushort FirstPaletteNum;
        }

        public struct Nclr
        {
            public NitroHeader Header;
            public PlttHeader Pltt;
            public PmcpHeader Pmcp;
        }

        public struct Ncgr  // Nintendo Character Graphic Resource
        {
            public NitroHeader Header;
            public Rahc Rahc;
            public Sopc Sopc;
            public TileForm Order;
            public object Other;
            public uint Id;
        }
        public struct Rahc  // CHARacter
        {
            public char[] Id;               // Always RAHC = 0x52414843
            public uint SizeSection;
            public ushort NTilesY;
            public ushort NTilesX;
            public ColorFormat Depth;
            public ushort Unknown1;
            public ushort Unknown2;
            public uint TiledFlag;
            public uint SizeTiledata;
            public uint Unknown3;         // Always 0x18 (24) (data offset?)
            public byte[] Data;             // image data

            public int Bpp;
        }

        public struct Sopc  // Unknown section
        {
            public char[] Id;
            public uint SizeSection;
            public uint Unknown1;
            public ushort CharSize;
            public ushort NChar;
        }
        public struct Ncer       // Nintendo CEll Resource
        {
            public NitroHeader header;
            public CEBK cebk;
            public LABL labl;
            public UEXT uext;
            public Program.Bank[] banks;
            public struct CEBK
            {
                public char[] id;
                public uint section_size;
                public ushort nBanks;
                public ushort tBank;            // type of banks, 0 ó 1
                public uint bank_data_offset;
                public uint block_size;
                public uint partition_data_offset;
                public ulong unused;         // Unused pointers to LABL and UEXT sections
                public Bank[] banks;

                public uint max_partition_size;
                public uint first_partition_data_offset;
            }
            public struct Bank
            {
                public ushort nCells;
                public ushort readOnlyCellInfo;
                public uint cell_offset;
                public uint partition_offset;
                public uint partition_size;
                public OAM[] oams;

                // Extended mode
                public short xMax;
                public short yMax;
                public short xMin;
                public short yMin;
            }

            public struct LABL
            {
                public char[] id;
                public uint section_size;
                public uint[] offset;
                public string[] names;
            }
            public struct UEXT
            {
                public char[] id;
                public uint section_size;
                public uint unknown;
            }
        }
        public struct Bank
        {
            public OAM[] oams;
            public string name;

            public ushort height;
            public ushort width;

            public uint data_offset;
            public uint data_size;
        }
        public struct Obj0  // 16 bits
        {
            public int yOffset;       // Bit0-7 -> signed
            public byte rs_flag;        // Bit8 -> Rotation / Scale flag
            public byte objDisable;     // Bit9 -> if r/s == 0
            public byte doubleSize;     // Bit9 -> if r/s != 0
            public byte objMode;        // Bit10-11 -> 0 = normal; 1 = semi-trans; 2 = window; 3 = invalid
            public byte mosaic_flag;    // Bit12 
            public byte depth;          // Bit13 -> 0 = 4bit; 1 = 8bit
            public byte shape;          // Bit14-15 -> 0 = square; 1 = horizontal; 2 = vertial; 3 = invalid
        }
        public struct Obj1  // 16 bits
        {
            public int xOffset;   // Bit0-8 (unsigned)

            // If R/S == 0
            public byte unused; // Bit9-11
            public byte flipX;  // Bit12
            public byte flipY;  // Bit13
            // If R/S != 0
            public byte select_param;   //Bit9-13 -> Parameter selection

            public byte size;   // Bit14-15
        }
        public struct Obj2  // 16 bits
        {
            public uint tileOffset;     // Bit0-9
            public byte priority;       // Bit10-11
            public byte index_palette;  // Bit12-15
        }
        public struct OAM
        {
            public Obj0 obj0;
            public Obj1 obj1;
            public Obj2 obj2;

            public ushort width;
            public ushort height;
            public ushort num_cell;
        }

        public class ProcessedNCGR
        {
            public string nclr;
            public Bgfx.TextureHandle textureId;
            public int paletteNo;

            public ProcessedNCGR(string nclr, Bgfx.TextureHandle textureId, int paletteNo)
            {
                this.paletteNo = paletteNo;
                this.nclr = nclr;
                this.textureId = textureId;
            }
        }

        public class ProcessedNCER
        {
            public string ncgr;
            public string nclr;
            public string paletteno;
            public Bgfx.TextureHandle textureId;
            public ProcessedNCER(string ncgr,string nclr, Bgfx.TextureHandle textureId, string paletteno)
            {
                this.textureId = textureId;
                this.nclr = nclr;
                this.ncgr = ncgr;
                this.paletteno = paletteno;
            }
        }

        private static void Main(string[] args)
        {
            var platform = "x64";
            if (IntPtr.Size == 4) platform = "x86";
            NativeMethods.LoadLibrary($"{platform}/SDL2.dll");
            Bgfx.InitializeLibrary();
            ushort resolutionWidth = 800;
            ushort resolutionHeight = 600;
            var windowhandle = SDL.SDL_CreateWindow("NDS Toolkit", 10, 10, resolutionWidth, resolutionHeight,
                SDL.SDL_WindowFlags.SDL_WINDOW_ALLOW_HIGHDPI | SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
            var wm = new SDL.SDL_SysWMinfo();
            SDL.SDL_GetWindowWMInfo(windowhandle, ref wm);
            Bgfx.SetPlatformData(wm.info.win.window);
            var init = Bgfx.Initialize(resolutionWidth, resolutionHeight, Bgfx.RendererType.DIRECT_3D11);
            var io = InitializeImGui(resolutionWidth, resolutionHeight, wm, out var cursors);
            Bgfx.SetViewClear(0, (ushort)(Bgfx.ClearFlags.COLOR | Bgfx.ClearFlags.DEPTH), 0x6495edff, 0, 0);
            Bgfx.SetViewMode(0, Bgfx.ViewMode.SEQUENTIAL);
            Bgfx.SetViewMode(255, Bgfx.ViewMode.SEQUENTIAL);
            Bgfx.SetDebug(Bgfx.DebugFlags.NONE);
            Bgfx.Reset(resolutionWidth, resolutionHeight, Bgfx.ResetFlags.VSYNC | Bgfx.ResetFlags.MSAA_X4, init.format);
            var running = true;
            var bgfxcaps = Bgfx.GetCaps();
            OrthographicMatrix(out var ortho, 0, resolutionWidth, resolutionHeight, 0, 0, 1000.0f, 0, bgfxcaps.Homogenousdepth);
            var imguiVertexLayout = new Bgfx.VertexLayout();
            imguiVertexLayout.Begin(Bgfx.RendererType.NOOP);
            imguiVertexLayout.Add(Bgfx.Attrib.POSITION, Bgfx.AttribType.FLOAT, 2, false, false);
            imguiVertexLayout.Add(Bgfx.Attrib.TEX_COORD0, Bgfx.AttribType.FLOAT, 2, false, false);
            imguiVertexLayout.Add(Bgfx.Attrib.COLOR0, Bgfx.AttribType.UINT8, 4, true, false);
            imguiVertexLayout.End();
            var whitePixelTexture = Bgfx.CreateTexture2D(1, 1, false, 1, Bgfx.TextureFormat.RGBA8, Bgfx.SamplerFlags.V_CLAMP | Bgfx.SamplerFlags.U_CLAMP | Bgfx.SamplerFlags.MIN_POINT | Bgfx.SamplerFlags.MAG_POINT | Bgfx.SamplerFlags.MIP_POINT, new uint[] { 0x0000ffff });
            var textureUniform = Bgfx.CreateUniform("s_texture", Bgfx.UniformType.SAMPLER, 1);
            var imGuiShader = LoadEffect("vs_imgui.bin", "fs_imgui.bin");

            var activeFiles = new Dictionary<string, ParsedFile>();
            var ProcessedNCERs = new Dictionary<string, ProcessedNCER>();
            var ProcessedNCGRs = new Dictionary<string, ProcessedNCGR>();
            bool fileParsed = false;
            var parsedFiles = new List<Tuple<ParsedFile, StringCollection>>();
            while (running)
            {
                SDLHandleEvents(ref running, init, io, bgfxcaps, windowhandle, cursors, ref resolutionWidth, ref resolutionHeight, ref ortho);

                ImGui.NewFrame();

                var openmodal = false;

                if (ImGui.BeginMainMenuBar())
                {
                    if (ImGui.BeginMenu("File"))
                    {
                        if (ImGui.MenuItem("Open", "Ctrl+O"))
                        {
                            openmodal = true;
                        }
                        ImGui.EndMenu();
                    }
                    ImGui.EndMainMenuBar();
                }

                var dummy = true;
                if (ImGui.BeginPopupModal("Open File"))
                {
                    var currentDirectory = Path.Combine(Directory.GetCurrentDirectory(), "content");
                    ImGui.Text($"Checking Directory: {currentDirectory}");
                    if (!fileParsed)
                    {
                        var files = Directory.GetFiles(currentDirectory);
                        for (int i = 0; i < files.Length; i++)
                        {
                            var file = File.OpenRead(files[i]);
                            var reader = new BinaryReader(file);
                            var nitroHeader = new NitroHeader();
                            nitroHeader.Magic = reader.ReadChars(4);
                            nitroHeader.Endianess = reader.ReadUInt16();
                            if (nitroHeader.Endianess == 0xFFFE)
                                nitroHeader.Magic.Reverse();
                            nitroHeader.Constant = reader.ReadUInt16();
                            nitroHeader.Sectionsize = reader.ReadUInt32();
                            nitroHeader.Headersize = reader.ReadUInt16();
                            nitroHeader.Sectioncount = reader.ReadUInt16();

                            bool CheckIfNclr(char[] magic)
                            {
                                // Little Endian so NCLR -> RLCN
                                return 'R' == magic[0] &&
                                       'L' == magic[1] &&
                                       'C' == magic[2] &&
                                       'N' == magic[3];
                            }

                            bool CheckIfNcgr(char[] magic)
                            {
                                // Little Endian so NCGR -> RGCN
                                return 'R' == magic[0] &&
                                       'G' == magic[1] &&
                                       'C' == magic[2] &&
                                       'N' == magic[3];
                            }

                            bool CheckIfNcer(char[] magic)
                            {
                                // Little Endian so NCGR -> RGCN
                                return 'R' == magic[0] &&
                                       'E' == magic[1] &&
                                       'C' == magic[2] &&
                                       'N' == magic[3];
                            }


                            {
                                var data = new StringCollection();
                                if (CheckIfNcgr(nitroHeader.Magic))
                                {
                                    var ncgr = ParseNcgr(nitroHeader, reader, data);
                                    parsedFiles.Add(new Tuple<ParsedFile, StringCollection>(new ParsedFile { Data = (object)ncgr, FullPath = files[i], type = FileTypes.NCGR }, data));
                                }

                                if (CheckIfNclr(nitroHeader.Magic))
                                {
                                    var nclr = ParseNclr(nitroHeader, reader, data);
                                    parsedFiles.Add(new Tuple<ParsedFile, StringCollection>(new ParsedFile { Data = (object)nclr, FullPath = files[i], type = FileTypes.NCLR }, data));
                                }

                                if (CheckIfNcer(nitroHeader.Magic))
                                {
                                    var ncer = ParseNcer(nitroHeader, reader, data);
                                    parsedFiles.Add(new Tuple<ParsedFile, StringCollection>(new ParsedFile { Data = (object)ncer, FullPath = files[i], type = FileTypes.NCER }, data));
                                }
                            }

                            file.Close();
                        }

                        fileParsed = true;
                    }

                    var closepopup = false;
                    parsedFiles.ForEach(x =>
                    {
                        if (ImGui.Button(Path.GetFileName(x.Item1.FullPath)))
                        {
                            if (!activeFiles.ContainsKey(x.Item1.FullPath))
                            {
                                activeFiles.Add(x.Item1.FullPath, x.Item1);
                            }
                            closepopup = true;
                        }
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            foreach (var text in x.Item2)
                            {
                                ImGui.BulletText(text);
                            }
                            ImGui.EndTooltip();
                        }
                    });
                    if (ImGui.Button("Close"))
                    {
                        closepopup = true;
                    }

                    if (closepopup)
                    {
                        parsedFiles.ForEach(x => x.Item2.Clear());
                        parsedFiles.Clear();
                        ImGui.CloseCurrentPopup();
                        fileParsed = false;
                    }
                    ImGui.EndPopup();
                }

                if (openmodal)
                {
                    ImGui.OpenPopup("Open File");
                }
                //ImGui.ShowDemoWindow();

                if (activeFiles.Count > 0)
                {
                    if (ImGui.Begin("Active Files"))
                    {
                        foreach (var activeFile in activeFiles.Values)
                        {
                            var name = Path.GetFileName(activeFile.FullPath);
                            ImGui.BulletText($"{name} type: {activeFile.type}");

                        }
                    }
                    ImGui.End();

                    foreach (var file in activeFiles.Values)
                    {
                        if (file.type == FileTypes.NCGR)
                        {
                            if (!ProcessedNCGRs.ContainsKey(file.FullPath))
                            {
                                var ncgr = (Ncgr)(file.Data);
                                var texture = Bgfx.CreateTexture2D(ncgr.Rahc.NTilesX, ncgr.Rahc.NTilesY, false, 1, Bgfx.TextureFormat.RGBA8, Bgfx.SamplerFlags.V_CLAMP | Bgfx.SamplerFlags.U_CLAMP | Bgfx.SamplerFlags.MIN_POINT | Bgfx.SamplerFlags.MAG_POINT | Bgfx.SamplerFlags.MIP_POINT, IntPtr.Zero);
                                ProcessedNCGRs.Add(file.FullPath, new ProcessedNCGR("", texture, 0));
                            }
                        }
                        if (file.type == FileTypes.NCER)
                        {
                            if (!ProcessedNCERs.ContainsKey(file.FullPath))
                            {
                                var ncer = (Ncer)(file.Data);
                                var texture = Bgfx.CreateTexture2D(ncgr.Rahc.NTilesX, ncgr.Rahc.NTilesY, false, 1, Bgfx.TextureFormat.RGBA8, Bgfx.SamplerFlags.V_CLAMP | Bgfx.SamplerFlags.U_CLAMP | Bgfx.SamplerFlags.MIN_POINT | Bgfx.SamplerFlags.MAG_POINT | Bgfx.SamplerFlags.MIP_POINT, IntPtr.Zero);
                                ProcessedNCERs.Add(file.FullPath, new ProcessedNCER("","", texture, 0));
                            }
                        }
                        var name = Path.GetFileName(file.FullPath);
                        if (ImGui.Begin($"{name} type: {file.type}", ImGuiWindowFlags.HorizontalScrollbar))
                        {
                            if (file.type == FileTypes.NCLR)
                            {
                                var nclr = (Nclr)file.Data;
                                for (int i = 0; i < nclr.Pltt.Palettes.Length; i++)
                                {
                                    var palette = nclr.Pltt.Palettes[i];
                                    ImGui.BulletText($"Palette {i + 1}:");
                                    for (int j = 0; j < palette.Length; j++)
                                    {
                                        ImGui.SameLine();
                                        var color = ImGui.ColorConvertU32ToFloat4(palette[j]);
                                        ImGui.ColorButton(" ", color);
                                    }
                                }
                            }

                            if (file.type == FileTypes.NCGR)
                            {
                                var ncgr = (Ncgr)file.Data;
                                var nclrs = activeFiles.Where(x => x.Value.type == FileTypes.NCLR);
                                var currentnclr = ProcessedNCGRs[file.FullPath].nclr;
                                ImGui.Text("Current NCLR");
                                ImGui.SameLine();
                                if (ImGui.BeginCombo("##NCLR", currentnclr))
                                {
                                    foreach (var nclr in nclrs)
                                    {
                                        bool selected = (currentnclr == nclr.Key); // You can store your selection however you want, outside or inside your objects
                                        if (ImGui.Selectable(nclr.Key, selected))
                                        {
                                            ProcessedNCGRs[file.FullPath].nclr = nclr.Key;
                                            var idx = 0;
                                            var pixels = NcgrToBitmap(ncgr, nclr, idx);
                                            Bgfx.UpdateTexture2D(ProcessedNCGRs[file.FullPath].textureId, 0, 0, 0, 0,
                                                ncgr.Rahc.NTilesX, ncgr.Rahc.NTilesY, pixels, ushort.MaxValue);
                                        }

                                        if (selected) ImGui.SetItemDefaultFocus();
                                    }

                                    ImGui.EndCombo();
                                }
                                ImGui.Text("Pallet No:");
                                ImGui.SameLine();
                                var currentpaletteno = ProcessedNCGRs[file.FullPath].paletteNo.ToString();
                                if (ImGui.BeginCombo("##PaltteNo", currentpaletteno))
                                {

                                    foreach (var nclr in nclrs)
                                    {

                                        if (nclr.Key == ProcessedNCGRs[file.FullPath].nclr)
                                        {
                                            for (int i = 0; i < ((Nclr)nclr.Value.Data).Pltt.Palettes.Length; i++)
                                            {
                                                var idx = i;
                                                bool selected = (idx == ProcessedNCGRs[file.FullPath].paletteNo);
                                                if (ImGui.Selectable(idx.ToString(), selected))
                                                {
                                                    ProcessedNCGRs[file.FullPath].paletteNo = idx;
                                                    var pixels = NcgrToBitmap(ncgr, nclr, idx);

                                                    Bgfx.UpdateTexture2D(ProcessedNCGRs[file.FullPath].textureId, 0, 0,
                                                        0, 0, ncgr.Rahc.NTilesX, ncgr.Rahc.NTilesY, pixels,
                                                        ushort.MaxValue);
                                                }

                                                if (selected) ImGui.SetItemDefaultFocus();
                                            }
                                        }
                                    }

                                    ImGui.EndCombo();
                                }


                                if (ImGui.TreeNode("Raw Tile Data"))
                                {

                                    for (int y = 0; y < ncgr.Rahc.NTilesY; y++)
                                    {
                                        ImGui.Text($"{y:D2} :");
                                        for (int x = 0; x < ncgr.Rahc.NTilesX; x++)
                                        {
                                            ImGui.SameLine();
                                            var tile = ncgr.Rahc.Data[y * (ncgr.Rahc.NTilesX / 2) + (x / 2)];
                                            var t1 = tile & 0xe;
                                            var t2 = (tile >> 4) & 0xe;
                                            ImGui.Text($"{t1:D2}");
                                            ImGui.SameLine();
                                            ImGui.Text($"{t1:D2}");
                                        }
                                    }
                                }

                                if (ImGui.TreeNode("Output Texture"))
                                {
                                    if (ProcessedNCGRs[file.FullPath].nclr == "")
                                    {
                                        ImGui.Text("Palette Not Loaded");
                                    }
                                    else
                                    {
                                        ImGui.Text($"Palette Loaded:{ProcessedNCGRs[file.FullPath].nclr}");
                                        ImGui.Image(new IntPtr(ProcessedNCGRs[file.FullPath].textureId.Idx), new Vector2(ncgr.Rahc.NTilesX, ncgr.Rahc.NTilesY));
                                        var nclr = nclrs.Where(x => x.Key == ProcessedNCGRs[file.FullPath].nclr).Single();
                                        if (ImGui.Button("Export To Png"))
                                        {
                                            using (Stream stream = File.OpenWrite(Path.ChangeExtension(Path.GetFileName(file.FullPath), ".png")))
                                            {
                                                var idx = ProcessedNCGRs[file.FullPath].paletteNo;
                                                var pixels = NcgrToBitmap(ncgr, nclr, idx).SelectMany(x => BitConverter.GetBytes(x)).ToArray();
                                                var writer = new ImageWriter();
                                                writer.WritePng(pixels, ncgr.Rahc.NTilesX, ncgr.Rahc.NTilesY, ColorComponents.RedGreenBlueAlpha, stream);
                                            }
                                        }
                                        if (ImGui.Button("Export To Bmp"))
                                        {
                                            using (Stream stream = File.OpenWrite(Path.ChangeExtension(Path.GetFileName(file.FullPath), ".bmp")))
                                            {
                                                var idx = ProcessedNCGRs[file.FullPath].paletteNo;
                                                var pixels = NcgrToBitmap(ncgr, nclr, idx).SelectMany(x => BitConverter.GetBytes(x)).ToArray();
                                                var writer = new ImageWriter();
                                                writer.WriteBmp(pixels, ncgr.Rahc.NTilesX, ncgr.Rahc.NTilesY, ColorComponents.RedGreenBlueAlpha, stream);
                                            }
                                        }
                                        if (ImGui.Button("Export To Tga"))
                                        {
                                            using (Stream stream = File.OpenWrite(Path.ChangeExtension(Path.GetFileName(file.FullPath), ".tga")))
                                            {
                                                var idx = ProcessedNCGRs[file.FullPath].paletteNo;
                                                var pixels = NcgrToBitmap(ncgr, nclr, idx).SelectMany(x => BitConverter.GetBytes(x)).ToArray();
                                                var writer = new ImageWriter();
                                                writer.WriteTga(pixels, ncgr.Rahc.NTilesX, ncgr.Rahc.NTilesY, ColorComponents.RedGreenBlueAlpha, stream);
                                            }
                                        }
                                        if (ImGui.Button("Export To Hdr"))
                                        {
                                            using (Stream stream = File.OpenWrite(Path.ChangeExtension(Path.GetFileName(file.FullPath), ".hdr")))
                                            {
                                                var idx = ProcessedNCGRs[file.FullPath].paletteNo;
                                                var pixels = NcgrToBitmap(ncgr, nclr, idx).SelectMany(x => BitConverter.GetBytes(x)).ToArray();
                                                var writer = new ImageWriter();
                                                writer.WriteHdr(pixels, ncgr.Rahc.NTilesX, ncgr.Rahc.NTilesY, ColorComponents.RedGreenBlueAlpha, stream);
                                            }
                                        }
                                        if (ImGui.Button("Export To Jpg"))
                                        {
                                            using (Stream stream = File.OpenWrite(Path.ChangeExtension(Path.GetFileName(file.FullPath), ".jpg")))
                                            {
                                                var idx = ProcessedNCGRs[file.FullPath].paletteNo;
                                                var pixels = NcgrToBitmap(ncgr, nclr, idx).SelectMany(x => BitConverter.GetBytes(x)).ToArray();
                                                var writer = new ImageWriter();
                                                writer.WriteJpg(pixels, ncgr.Rahc.NTilesX, ncgr.Rahc.NTilesY, ColorComponents.RedGreenBlueAlpha, stream, 1);
                                            }
                                        }
                                    }
                                }
                            }

                            if (file.type == FileTypes.NCGR)
                            {
                                var ncer = (Ncer) file.Data;

                            }
                        }
                        ImGui.End();
                    }
                }

                ImGui.End();
                ImGui.EndFrame();
                ImGui.Render();
                Bgfx.SetViewRect(0, 0, 0, resolutionWidth, resolutionHeight);
                Bgfx.SetViewRect(255, 0, 0, resolutionWidth, resolutionHeight);
                Bgfx.Touch(0);
                DrawImGuiUI(ortho, imguiVertexLayout, whitePixelTexture, textureUniform, imGuiShader);
                Bgfx.Frame(false);
            }
        }

        private static Ncer ParseNcer(NitroHeader nitroHeader, BinaryReader reader, StringCollection data)
        {
            var ncer = new Ncer();
            ncer.header = nitroHeader;
            ncer.cebk.id = reader.ReadChars(4);
            ncer.cebk.section_size = reader.ReadUInt32();
            ncer.cebk.nBanks = reader.ReadUInt16();
            ncer.cebk.tBank = reader.ReadUInt16();
            ncer.cebk.bank_data_offset = reader.ReadUInt32();
            ncer.cebk.block_size = reader.ReadUInt32() & 0xFF;
            ncer.cebk.partition_data_offset = reader.ReadUInt32();
            ncer.cebk.unused = reader.ReadUInt64();
            ncer.cebk.banks = new Ncer.Bank[ncer.cebk.nBanks];
            if (ncer.cebk.partition_data_offset != 0)
            {
                reader.BaseStream.Position =
                    ncer.header.Headersize + ncer.cebk.partition_data_offset +
                    8; // 8 is a CEBK general header size (magic and size)
                ncer.cebk.max_partition_size = reader.ReadUInt32();
                ncer.cebk.first_partition_data_offset = reader.ReadUInt32();
                reader.BaseStream.Position += ncer.cebk.first_partition_data_offset - 8;
                for (int p = 0; p < ncer.cebk.nBanks; p++)
                {
                    ncer.cebk.banks[p].partition_offset = reader.ReadUInt32();
                    ncer.cebk.banks[p].partition_size = reader.ReadUInt32();
                }
            }

            reader.BaseStream.Position = ncer.header.Headersize + ncer.cebk.bank_data_offset + 8;

            #region Read banks
            for (int i = 0; i < ncer.cebk.nBanks; i++)
            {
                ncer.cebk.banks[i].nCells = reader.ReadUInt16();
                ncer.cebk.banks[i].readOnlyCellInfo = reader.ReadUInt16();
                ncer.cebk.banks[i].cell_offset = reader.ReadUInt32();

                if (ncer.cebk.tBank == 0x01)
                {
                    ncer.cebk.banks[i].xMax = reader.ReadInt16();
                    ncer.cebk.banks[i].yMax = reader.ReadInt16();
                    ncer.cebk.banks[i].xMin = reader.ReadInt16();
                    ncer.cebk.banks[i].yMin = reader.ReadInt16();
                }

                long posicion = reader.BaseStream.Position;

                if (ncer.cebk.tBank == 0x00)
                    reader.BaseStream.Position += (ncer.cebk.nBanks - (i + 1)) * 8 + ncer.cebk.banks[i].cell_offset;
                else
                    reader.BaseStream.Position += (ncer.cebk.nBanks - (i + 1)) * 0x10 + ncer.cebk.banks[i].cell_offset;


                ncer.cebk.banks[i].oams = new OAM[ncer.cebk.banks[i].nCells];

                #region Read cells
                for (int j = 0; j < ncer.cebk.banks[i].nCells; j++)
                {
                    ushort obj0 = reader.ReadUInt16();
                    ushort obj1 = reader.ReadUInt16();
                    ushort obj2 = reader.ReadUInt16();

                    ncer.cebk.banks[i].oams[j] = OAMInfo(new ushort[] { obj0, obj1, obj2 });
                    ncer.cebk.banks[i].oams[j].num_cell = (ushort)j;

                    // Calculate the size
                    Vector2 cellSize = Get_OAMSize(ncer.cebk.banks[i].oams[j].obj0.shape, ncer.cebk.banks[i].oams[j].obj1.size);
                    ncer.cebk.banks[i].oams[j].height = (ushort)cellSize.X;
                    ncer.cebk.banks[i].oams[j].width = (ushort)cellSize.Y;
                }
                #endregion

                // Sort the oam using the priority value
                List<OAM> oams = new List<OAM>();
                oams.AddRange(ncer.cebk.banks[i].oams);
                oams.Sort(Comparision_Cell);
                ncer.cebk.banks[i].oams = oams.ToArray();

                reader.BaseStream.Position = posicion;
            }

            #endregion

            #region LABL
            reader.BaseStream.Position = ncer.header.Headersize + ncer.cebk.section_size;
            List<uint> offsets = new List<uint>();
            List<string> names = new List<string>();
            ncer.labl.names = new string[ncer.cebk.nBanks];

            ncer.labl.id = reader.ReadChars(4);
            if (new string(ncer.labl.id) != "LBAL")
                goto Tercera;
            ncer.labl.section_size = reader.ReadUInt32();

            // Name offset
            for (int i = 0; i < ncer.cebk.nBanks; i++)
            {
                uint offset = reader.ReadUInt32();
                if (offset >= ncer.labl.section_size - 8)
                {
                    reader.BaseStream.Position -= 4;
                    break;
                }

                offsets.Add(offset);
            }
            ncer.labl.offset = offsets.ToArray();

            // Names
            for (int i = 0; i < ncer.labl.offset.Length; i++)
            {
                names.Add("");
                byte c = reader.ReadByte();
                while (c != 0x00)
                {
                    names[i] += (char)c;
                    c = reader.ReadByte();
                }
            }
            Tercera:
                for (int i = 0; i < ncer.cebk.nBanks; i++)
                    if (names.Count > i)
                        ncer.labl.names[i] = names[i];
                    else
                        ncer.labl.names[i] = i.ToString();
            #endregion

            #region UEXT
            ncer.uext.id = reader.ReadChars(4);
            if (new string(ncer.uext.id) != "TXEU")
                goto Fin;

            ncer.uext.section_size = reader.ReadUInt32();
            ncer.uext.unknown = reader.ReadUInt32();
            #endregion

            Fin:
                reader.Close();
                ncer.banks = Set_Banks(Convert_Banks(ncer), ncer.cebk.block_size, true);

            return ncer;
        }
        private static Bank[] Set_Banks(Bank[] banks, uint block_size, bool editable)
        {
            // Sort the cell using the priority value
            for (int b = 0; b < banks.Length; b++)
            {
                List<OAM> cells = new List<OAM>();
                cells.AddRange(banks[b].oams);
                cells.Sort(Comparision_OAM);
                banks[b].oams = cells.ToArray();
            }
            return banks;
        }
        public static int Comparision_OAM(OAM c1, OAM c2)
        {
            if (c1.obj2.priority < c2.obj2.priority)
                return 1;
            if (c1.obj2.priority > c2.obj2.priority)
                return -1;
            if (c1.num_cell < c2.num_cell)
                return 1;
            if (c1.num_cell > c2.num_cell)
                return -1;
            return 0;
        }

        private static Bank[] Convert_Banks(Ncer ncer)
        {
            Bank[] banks = new Bank[ncer.cebk.banks.Length];
            for (int i = 0; i < banks.Length; i++)
            {
                banks[i].height = 0;
                banks[i].width = 0;
                banks[i].oams = ncer.cebk.banks[i].oams;
                if (ncer.labl.names.Length > i)
                    banks[i].name = ncer.labl.names[i];

                banks[i].data_offset = ncer.cebk.banks[i].partition_offset;
                banks[i].data_size = ncer.cebk.banks[i].partition_size;
            }
            return banks;
        }

        private static int Comparision_Cell(OAM c1, OAM c2)
        {
            if (c1.obj2.priority < c2.obj2.priority)
                return 1;
            if (c1.obj2.priority > c2.obj2.priority)
                return -1;
            if (c1.num_cell < c2.num_cell)
                return 1;
            if (c1.num_cell > c2.num_cell)
                return -1;
            return 0;
        }

        private static Vector2 Get_OAMSize(byte shape, byte size)
        {
            Vector2 imageSize = new Vector2();
            switch (shape)
            {
                case 0x00:  // Square
                    switch (size)
                    {
                        case 0x00:
                            imageSize = new Vector2(8, 8);
                            break;
                        case 0x01:
                            imageSize = new Vector2(16, 16);
                            break;
                        case 0x02:
                            imageSize = new Vector2(32, 32);
                            break;
                        case 0x03:
                            imageSize = new Vector2(64, 64);
                            break;
                    }
                    break;
                case 0x01:  // Horizontal
                    switch (size)
                    {
                        case 0x00:
                            imageSize = new Vector2(16, 8);
                            break;
                        case 0x01:
                            imageSize = new Vector2(32, 8);
                            break;
                        case 0x02:
                            imageSize = new Vector2(32, 16);
                            break;
                        case 0x03:
                            imageSize = new Vector2(64, 32);
                            break;
                    }
                    break;
                case 0x02:  // Vertical
                    switch (size)
                    {
                        case 0x00:
                            imageSize = new Vector2(8, 16);
                            break;
                        case 0x01:
                            imageSize = new Vector2(8, 32);
                            break;
                        case 0x02:
                            imageSize = new Vector2(16, 32);
                            break;
                        case 0x03:
                            imageSize = new Vector2(32, 64);
                            break;
                    }
                    break;
            }

            return imageSize;
        }


        private static OAM OAMInfo(ushort[] obj)
        {
            OAM oam = new OAM();

            // Obj 0
            oam.obj0.yOffset = (sbyte)(obj[0] & 0xFF);
            oam.obj0.rs_flag = (byte)((obj[0] >> 8) & 1);
            if (oam.obj0.rs_flag == 0)
                oam.obj0.objDisable = (byte)((obj[0] >> 9) & 1);
            else
                oam.obj0.doubleSize = (byte)((obj[0] >> 9) & 1);
            oam.obj0.objMode = (byte)((obj[0] >> 10) & 3);
            oam.obj0.mosaic_flag = (byte)((obj[0] >> 12) & 1);
            oam.obj0.depth = (byte)((obj[0] >> 13) & 1);
            oam.obj0.shape = (byte)((obj[0] >> 14) & 3);

            // Obj 1
            oam.obj1.xOffset = obj[1] & 0x01FF;
            if (oam.obj1.xOffset >= 0x100)
                oam.obj1.xOffset -= 0x200;
            if (oam.obj0.rs_flag == 0)
            {
                oam.obj1.unused = (byte)((obj[1] >> 9) & 7);
                oam.obj1.flipX = (byte)((obj[1] >> 12) & 1);
                oam.obj1.flipY = (byte)((obj[1] >> 13) & 1);
            }
            else
                oam.obj1.select_param = (byte)((obj[1] >> 9) & 0x1F);
            oam.obj1.size = (byte)((obj[1] >> 14) & 3);

            // Obj 2
            oam.obj2.tileOffset = (uint)(obj[2] & 0x03FF);
            oam.obj2.priority = (byte)((obj[2] >> 10) & 3);
            oam.obj2.index_palette = (byte)((obj[2] >> 12) & 0xF);

            Vector2 size = Get_OAMSize(oam.obj0.shape, oam.obj1.size);
            oam.width = (ushort)size.X;
            oam.height = (ushort)size.Y;

            return oam;
        }

        private static Ncgr ParseNcgr(NitroHeader nitroHeader, BinaryReader reader, StringCollection data)
        {
            var ncgr = new Ncgr();

            // Read the common header
            ncgr.Header = nitroHeader;

            // Read the first section: CHAR (CHARacter data)
            ncgr.Rahc.Id = reader.ReadChars(4);
            ncgr.Rahc.SizeSection = reader.ReadUInt32();
            ncgr.Rahc.NTilesY = reader.ReadUInt16();
            ncgr.Rahc.NTilesX = reader.ReadUInt16();
            ncgr.Rahc.Depth = (ColorFormat)reader.ReadUInt32();
            ncgr.Rahc.Unknown1 = reader.ReadUInt16();
            ncgr.Rahc.Unknown2 = reader.ReadUInt16();
            ncgr.Rahc.TiledFlag = reader.ReadUInt32();
            if ((ncgr.Rahc.TiledFlag & 0xFF) == 0x0)
                ncgr.Order = TileForm.Horizontal;
            else
                ncgr.Order = TileForm.Lineal;

            ncgr.Rahc.SizeTiledata = reader.ReadUInt32();
            ncgr.Rahc.Unknown3 = reader.ReadUInt32();
            ncgr.Rahc.Data = reader.ReadBytes((int)ncgr.Rahc.SizeTiledata);

            if (ncgr.Rahc.NTilesX != 0xFFFF)
            {
                ncgr.Rahc.NTilesX *= 8;
                ncgr.Rahc.NTilesY *= 8;
            }

            if (ncgr.Header.Sectioncount == 2 && reader.BaseStream.Position < reader.BaseStream.Length
            ) // If there isn't SOPC section
            {
                // Read the second section: SOPC
                ncgr.Sopc.Id = reader.ReadChars(4);
                ncgr.Sopc.SizeSection = reader.ReadUInt32();
                ncgr.Sopc.Unknown1 = reader.ReadUInt32();
                ncgr.Sopc.CharSize = reader.ReadUInt16();
                ncgr.Sopc.NChar = reader.ReadUInt16();
            }

            if (ncgr.Rahc.NTilesX == 0xFFFF)
            {
                int width, height;
                var format = ncgr.Rahc.Depth;
                ncgr.Rahc.Bpp = 8;
                if (format == ColorFormat.Colors16)
                    ncgr.Rahc.Bpp = 4;
                else if (format == ColorFormat.Colors2)
                    ncgr.Rahc.Bpp = 1;
                else if (format == ColorFormat.Colors4)
                    ncgr.Rahc.Bpp = 2;
                else if (format == ColorFormat.Direct)
                    ncgr.Rahc.Bpp = 16;
                else if (format == ColorFormat.Bgra32 || format == ColorFormat.Abgr32)
                    ncgr.Rahc.Bpp = 32;
                int num_pix = ((int)ncgr.Rahc.SizeTiledata) * 8 / ncgr.Rahc.Bpp;

                // If the image it's a square
                if (Math.Pow((int)(Math.Sqrt(num_pix)), 2) == num_pix)
                    width = height = (int)Math.Sqrt(num_pix);
                else
                {
                    width = (num_pix < 0x100 ? num_pix : 0x0100);
                    height = num_pix / width;
                }

                if (height == 0)
                    height = 1;
                if (width == 0)
                    width = 1;

                ncgr.Rahc.NTilesX = (ushort)width;
                ncgr.Rahc.NTilesY = (ushort)height;
            }

            {
                var bpp = 4;
                var tileSize = 8;
                byte[] horizontal = new byte[ncgr.Rahc.Data.Length];
                int tileWidth = (tileSize * bpp) / 8; // Calculate the number of byte per line in the tile
                // pixels per line * bits per pixel / 8 bits per byte
                var tilesX = ncgr.Rahc.NTilesX / tileSize;
                var tilesY = ncgr.Rahc.NTilesY / tileSize;
                int pos = 0;
                for (int ht = 0; ht < tilesY; ht++)
                {
                    for (int wt = 0; wt < tilesX; wt++)
                    {
                        // Get the tile data
                        for (int h = 0; h < tileSize; h++)
                        {
                            for (int w = 0; w < tileWidth; w++)
                            {
                                if ((w + h * tileWidth * tilesX) + wt * tileWidth + ht * tilesX * tileSize * tileWidth >=
                                    ncgr.Rahc.Data.Length)
                                    continue;
                                if (pos >= ncgr.Rahc.Data.Length)
                                    continue;
                                horizontal[(w + h * tileWidth * tilesX) + wt * tileWidth + ht * tilesX * tileSize * tileWidth] =
                                    ncgr.Rahc.Data[pos++];
                            }
                        }
                    }
                }

                ncgr.Rahc.Data = horizontal;
            }
            data.Add($"Tile Bit Depth :{ncgr.Rahc.Depth}");
            data.Add($"Tiles Count X:{ncgr.Rahc.NTilesX} x Y:{ncgr.Rahc.NTilesY}");
            data.Add($"Tiles Byte Size :{ncgr.Rahc.SizeTiledata}");
            data.Add($"Tilemap Order :{ncgr.Order}");
            return ncgr;
        }

        private static Nclr ParseNclr(NitroHeader nitroHeader, BinaryReader reader, StringCollection data)
        {
            var nclr = new Nclr();
            nclr.Header = nitroHeader;
            var magic = reader.ReadChars(4);
            var pltt = new PlttHeader();
            pltt.Magic = magic;
            pltt.Length = reader.ReadUInt32();
            pltt.Depth = (ColorFormat)reader.ReadUInt16();
            pltt.Unknown1 = reader.ReadUInt16();
            pltt.Unknown2 = reader.ReadUInt32();
            pltt.PaletteLength = reader.ReadUInt32();
            if (pltt.PaletteLength == 0 || pltt.PaletteLength > pltt.Length)
                pltt.PaletteLength = pltt.Length - 0x18;

            uint colorsStartOffset = reader.ReadUInt32();
            pltt.NumColors = (uint)((pltt.Depth == ColorFormat.Colors16) ? 0x10 : 0x100);
            if (pltt.PaletteLength / 2 < pltt.NumColors)
                pltt.NumColors = pltt.PaletteLength / 2;
            pltt.Palettes = new uint[pltt.PaletteLength / (pltt.NumColors * 2)][];
            reader.BaseStream.Position = 0x18 + colorsStartOffset;
            for (int p = 0; p < pltt.Palettes.Length; p++)
            {
                pltt.Palettes[p] = new uint[pltt.NumColors];
                for (int q = 0; q < pltt.NumColors; q++)
                {
                    pltt.Palettes[p][q] = Bgr555ToABGR(reader.ReadByte(), reader.ReadByte());
                }
            }

            data.Add($"Palette Bit Depth :{pltt.Depth}");
            data.Add($"Palette Color Count :{pltt.Palettes.Length * pltt.NumColors}");
            data.Add($"Palette Byte Length :{pltt.PaletteLength}");
            data.Add($"Palette Count :{pltt.Palettes.Length}");
            nclr.Pltt = pltt;
            if (nitroHeader.Sectioncount > 1)
            {
                // Parse PMCP
                var pmcpHeader = new PmcpHeader();
                pmcpHeader.Magic = reader.ReadChars(4);
                pmcpHeader.Sectionsize = reader.ReadUInt32();
                pmcpHeader.Palettecount = reader.ReadUInt16();
                pmcpHeader.Unknown0 = reader.ReadUInt16();
                pmcpHeader.Unknown1 = reader.ReadUInt32();
                pmcpHeader.FirstPaletteNum = reader.ReadUInt16();
                nclr.Pmcp = pmcpHeader;
            }

            return nclr;
        }

        private static uint[] NcgrToBitmap(Ncgr ncgr, KeyValuePair<string, ParsedFile> nclr, int idx)
        {
            var size = ncgr.Rahc.NTilesX * ncgr.Rahc.NTilesY;
            var pixels = new uint[size];
            var currentNclrData = ((Nclr)nclr.Value.Data);
            var palette = currentNclrData.Pltt.Palettes[idx];
            for (int y = 0; y < ncgr.Rahc.NTilesY; y++)
            {
                for (int x = 0; x < ncgr.Rahc.NTilesX; x++)
                {
                    var tile = ncgr.Rahc.Data[
                        y * (ncgr.Rahc.NTilesX / 2) + (x / 2)];
                    var t1 = (tile & 0x0000000f);
                    var t2 = ((tile >> 4) & 0x0000000f);
                    var index = x % 2 == 0 ? t1 : t2;
                    var outPixel = (palette[index]);
                    pixels[(y * ncgr.Rahc.NTilesX) + x] = outPixel;
                }
            }

            return pixels;
        }

        private static ImGuiIOPtr InitializeImGui(ushort resolutionWidth, ushort resolutionHeight, SDL.SDL_SysWMinfo wm,
            out IntPtr[] cursors)
        {
            ImGui.SetCurrentContext(ImGui.CreateContext());
            var io = ImGui.GetIO();
            io.DisplaySize = new Vector2(resolutionWidth, resolutionHeight);
            io.DisplayFramebufferScale = new Vector2(1);
            cursors = new IntPtr[(int)ImGuiMouseCursor.COUNT];
            cursors[(int)ImGuiMouseCursor.Arrow] = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_ARROW);
            cursors[(int)ImGuiMouseCursor.Hand] = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_HAND);
            cursors[(int)ImGuiMouseCursor.TextInput] =
                SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_IBEAM);
            cursors[(int)ImGuiMouseCursor.NotAllowed] = SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_NO);
            cursors[(int)ImGuiMouseCursor.ResizeAll] =
                SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEALL);
            cursors[(int)ImGuiMouseCursor.ResizeEW] =
                SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZEWE);
            cursors[(int)ImGuiMouseCursor.ResizeNESW] =
                SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENESW);
            cursors[(int)ImGuiMouseCursor.ResizeNWSE] =
                SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENWSE);
            cursors[(int)ImGuiMouseCursor.ResizeNS] =
                SDL.SDL_CreateSystemCursor(SDL.SDL_SystemCursor.SDL_SYSTEM_CURSOR_SIZENS);
            io.ImeWindowHandle = wm.info.win.window;
            io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors; // We can honor GetMouseCursor() values (optional)
            io.BackendFlags |=
                ImGuiBackendFlags.HasSetMousePos; // We can honor io.WantSetMousePos requests (optional, rarely used)
            //IO.BackendPlatformName = new NullTerminatedString("imgui_impl_win32".ToCharArray());
            io.KeyMap[(int)ImGuiKey.Tab] = (int)SDL.SDL_Scancode.SDL_SCANCODE_TAB;
            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)SDL.SDL_Scancode.SDL_SCANCODE_LEFT;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int)SDL.SDL_Scancode.SDL_SCANCODE_RIGHT;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int)SDL.SDL_Scancode.SDL_SCANCODE_UP;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int)SDL.SDL_Scancode.SDL_SCANCODE_DOWN;
            io.KeyMap[(int)ImGuiKey.PageUp] = (int)SDL.SDL_Scancode.SDL_SCANCODE_PAGEUP;
            io.KeyMap[(int)ImGuiKey.PageDown] = (int)SDL.SDL_Scancode.SDL_SCANCODE_PAGEDOWN;
            io.KeyMap[(int)ImGuiKey.Home] = (int)SDL.SDL_Scancode.SDL_SCANCODE_HOME;
            io.KeyMap[(int)ImGuiKey.End] = (int)SDL.SDL_Scancode.SDL_SCANCODE_END;
            io.KeyMap[(int)ImGuiKey.Insert] = (int)SDL.SDL_Scancode.SDL_SCANCODE_INSERT;
            io.KeyMap[(int)ImGuiKey.Delete] = (int)SDL.SDL_Scancode.SDL_SCANCODE_DELETE;
            io.KeyMap[(int)ImGuiKey.Backspace] = (int)SDL.SDL_Scancode.SDL_SCANCODE_BACKSPACE;
            io.KeyMap[(int)ImGuiKey.Space] = (int)SDL.SDL_Scancode.SDL_SCANCODE_SPACE;
            io.KeyMap[(int)ImGuiKey.Enter] = (int)SDL.SDL_Scancode.SDL_SCANCODE_KP_ENTER;
            io.KeyMap[(int)ImGuiKey.Escape] = (int)SDL.SDL_Scancode.SDL_SCANCODE_ESCAPE;
            io.KeyMap[(int)ImGuiKey.KeyPadEnter] = (int)SDL.SDL_Scancode.SDL_SCANCODE_RETURN;
            io.KeyMap[(int)ImGuiKey.A] = (int)SDL.SDL_Scancode.SDL_SCANCODE_A;
            io.KeyMap[(int)ImGuiKey.C] = (int)SDL.SDL_Scancode.SDL_SCANCODE_C;
            io.KeyMap[(int)ImGuiKey.V] = (int)SDL.SDL_Scancode.SDL_SCANCODE_V;
            io.KeyMap[(int)ImGuiKey.X] = (int)SDL.SDL_Scancode.SDL_SCANCODE_X;
            io.KeyMap[(int)ImGuiKey.Y] = (int)SDL.SDL_Scancode.SDL_SCANCODE_Y;
            io.KeyMap[(int)ImGuiKey.Z] = (int)SDL.SDL_Scancode.SDL_SCANCODE_Z;
            io.Fonts.AddFontDefault();
            io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out var fwidth, out var fheight);
            io.Fonts.SetTexID(new IntPtr(Bgfx.CreateTexture2D((ushort)fwidth, (ushort)fheight, false, 1,
                (Bgfx.TextureFormat)Bgfx.TextureFormat.RGBA8,
                (Bgfx.SamplerFlags.U_CLAMP | Bgfx.SamplerFlags.V_CLAMP | Bgfx.SamplerFlags.MIN_POINT |
                 Bgfx.SamplerFlags.MAG_POINT | Bgfx.SamplerFlags.MIP_POINT),
                Bgfx.MakeRef(pixels, (uint)(4 * fwidth * fheight))).Idx));
            return io;
        }

        private static uint Bgr555ToABGR(byte byte1, byte byte2)
        {
            int r, b, g;
            short bgr = BitConverter.ToInt16(new byte[] { byte1, byte2 }, 0);

            r = (bgr & 0x001F) * 0x08;
            g = ((bgr & 0x03E0) >> 5) * 0x08;
            b = ((bgr & 0x7C00) >> 10) * 0x08;
            r &= 0xff;
            g &= 0xff;
            b &= 0xff;
            b <<= 16;
            g <<= 8;
            var a = 0xff;
            a <<= 24;
            var result = (uint)(r | g | b | a);
            return result;
        }

        private static uint ABGRToBGRA(uint abgr)
        {
            uint r, b, g, a;
            r = (abgr & 0x000000ff);
            g = (abgr & 0x0000ff00);
            b = (abgr & 0x00ff0000);
            a = (abgr & 0xff000000);
            a >>= 24;
            g <<= 8;
            b <<= 8;
            r <<= 8;
            var result = r | b | g | a;
            return result;

        }

        private static void DrawImGuiUI(float[] ortho, Bgfx.VertexLayout imguiVertexLayout, Bgfx.TextureHandle whitePixelTexture,
            Bgfx.Uniform textureUniform, Effect imGuiShader)
        {
            var drawdata = ImGui.GetDrawData();
            ushort viewId = 0;
            Bgfx.SetViewTransform(viewId, null, ortho);

            {
                for (int ii = 0, num = drawdata.CmdListsCount; ii < num; ++ii)
                {
                    var drawList = drawdata.CmdListsRange[ii];
                    var numVertices = drawList.VtxBuffer.Size;
                    var numIndices = drawList.IdxBuffer.Size;
                    var tib = Bgfx.AllocateTransientIndexBuffer((uint)numIndices);
                    var tvb = Bgfx.AllocateTransientVertexBuffer((uint)numVertices, imguiVertexLayout);
                    tvb.CopyIntoBuffer(drawList.VtxBuffer.Data,
                        (uint)numVertices * (uint)Unsafe.SizeOf<ImDrawVert>());
                    tib.CopyIntoBuffer(drawList.IdxBuffer.Data,
                        (uint)numIndices * (uint)Unsafe.SizeOf<ushort>());
                    uint offset = 0;
                    for (int i = 0; i < drawList.CmdBuffer.Size; i++)
                    {
                        var cmd = drawList.CmdBuffer[i];
                        if (cmd.UserCallback != IntPtr.Zero)
                        {
                            // cmd->UserCallback(drawList, cmd);
                        }
                        else if (cmd.ElemCount > 0)
                        {
                            var state = Bgfx.StateFlags.WRITE_RGB
                                        | Bgfx.StateFlags.WRITE_A
                                        | Bgfx.StateFlags.MSAA;

                            var texHandle = new Bgfx.TextureHandle();
                            if (cmd.TextureId.ToInt32() != 0)
                            {
                                texHandle.Idx = (ushort)cmd.TextureId.ToInt32();
                            }
                            else
                            {
                                texHandle.Idx = whitePixelTexture.Idx;
                            }

                            state |= Bgfx.STATE_BLEND_FUNC(Bgfx.StateFlags.BLEND_SRC_ALPHA,
                                Bgfx.StateFlags.BLEND_INV_SRC_ALPHA);
                            ushort xx = (ushort)((cmd.ClipRect.X > 0.0f ? cmd.ClipRect.X : 0.0f));
                            ushort yy = (ushort)((cmd.ClipRect.Y > 0.0f ? cmd.ClipRect.Y : 0.0f));
                            ushort zz = (ushort)((cmd.ClipRect.Z > 65535.0f ? 65535.0f : cmd.ClipRect.Z) - xx);
                            ushort ww = (ushort)((cmd.ClipRect.W > 65535.0f ? 65535.0f : cmd.ClipRect.W) - yy);
                            Bgfx.SetScissor(xx, yy, zz, ww);
                            Bgfx.SetState(state, 0);
                            Bgfx.SetTexture(0, textureUniform, texHandle);
                            Bgfx.SetTransientVertexBuffer(0, tvb, 0, (uint)numVertices);
                            Bgfx.SetTransientIndexBuffer(tib, offset, cmd.ElemCount);
                            Bgfx.Submit(viewId, imGuiShader.Program, 0, false);
                        }

                        offset += cmd.ElemCount;
                    }
                }
            }
        }

        private static unsafe void SDLHandleEvents(ref bool running, Bgfx.Init init, ImGuiIOPtr io, Bgfx.BgfxCaps bgfxcaps,
            IntPtr windowhandle, IntPtr[] cursors, ref ushort resolutionWidth, ref ushort resolutionHeight, ref float[] ortho)
        {
            while (SDL.SDL_PollEvent(out var @event) != 0)
            {
                switch (@event.type)
                {
                    case SDL.SDL_EventType.SDL_FIRSTEVENT:
                        break;
                    case SDL.SDL_EventType.SDL_QUIT:
                        running = false;
                        break;
                    case SDL.SDL_EventType.SDL_APP_TERMINATING:
                        break;
                    case SDL.SDL_EventType.SDL_APP_LOWMEMORY:
                        break;
                    case SDL.SDL_EventType.SDL_APP_WILLENTERBACKGROUND:
                        break;
                    case SDL.SDL_EventType.SDL_APP_DIDENTERBACKGROUND:
                        break;
                    case SDL.SDL_EventType.SDL_APP_WILLENTERFOREGROUND:
                        break;
                    case SDL.SDL_EventType.SDL_APP_DIDENTERFOREGROUND:
                        break;
                    case SDL.SDL_EventType.SDL_DISPLAYEVENT:
                        break;
                    case SDL.SDL_EventType.SDL_WINDOWEVENT:
                        switch (@event.window.windowEvent)
                        {
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_NONE:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_SHOWN:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_HIDDEN:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_EXPOSED:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_MOVED:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_SIZE_CHANGED:
                                resolutionWidth = (ushort)@event.window.data1;
                                resolutionHeight = (ushort)@event.window.data2;
                                Bgfx.Reset(resolutionWidth, resolutionHeight,
                                    Bgfx.ResetFlags.VSYNC | Bgfx.ResetFlags.MSAA_X4, init.format);
                                io.DisplaySize = new Vector2(resolutionWidth, resolutionHeight);
                                OrthographicMatrix(out ortho, 0, resolutionWidth, resolutionHeight, 0, 0, 1000.0f,
                                    0, bgfxcaps.Homogenousdepth);
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_MINIMIZED:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_MAXIMIZED:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESTORED:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_ENTER:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_LEAVE:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_GAINED:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_LOST:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_CLOSE:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_TAKE_FOCUS:
                                break;
                            case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_HIT_TEST:
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        break;
                    case SDL.SDL_EventType.SDL_SYSWMEVENT:
                        break;
                    case SDL.SDL_EventType.SDL_KEYDOWN:
                    case SDL.SDL_EventType.SDL_KEYUP:
                        int key = (int)@event.key.keysym.scancode;
                        io.KeysDown[key] = @event.type == SDL.SDL_EventType.SDL_KEYDOWN;
                        io.KeyShift = ((SDL.SDL_GetModState() & SDL.SDL_Keymod.KMOD_SHIFT) != 0);
                        io.KeyCtrl = ((SDL.SDL_GetModState() & SDL.SDL_Keymod.KMOD_CTRL) != 0);
                        io.KeyAlt = ((SDL.SDL_GetModState() & SDL.SDL_Keymod.KMOD_ALT) != 0);
                        break;
                    case SDL.SDL_EventType.SDL_TEXTEDITING:
                        break;
                    case SDL.SDL_EventType.SDL_TEXTINPUT:
                        unsafe
                        {
                            io.AddInputCharactersUTF8(SDL.UTF8_ToManaged(new IntPtr(@event.text.text)));
                        }

                        break;
                    case SDL.SDL_EventType.SDL_KEYMAPCHANGED:
                        break;
                    case SDL.SDL_EventType.SDL_MOUSEMOTION:
                        break;
                    case SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN:
                        break;
                    case SDL.SDL_EventType.SDL_MOUSEBUTTONUP:
                        break;
                    case SDL.SDL_EventType.SDL_MOUSEWHEEL:
                        if (@event.wheel.x > 0) io.MouseWheelH += 1;
                        if (@event.wheel.y > 0) io.MouseWheel += 1;
                        if (@event.wheel.x < 0) io.MouseWheelH -= 1;
                        if (@event.wheel.y < 0) io.MouseWheel -= 1;
                        break;
                    case SDL.SDL_EventType.SDL_JOYAXISMOTION:
                        break;
                    case SDL.SDL_EventType.SDL_JOYBALLMOTION:
                        break;
                    case SDL.SDL_EventType.SDL_JOYHATMOTION:
                        break;
                    case SDL.SDL_EventType.SDL_JOYBUTTONDOWN:
                        break;
                    case SDL.SDL_EventType.SDL_JOYBUTTONUP:
                        break;
                    case SDL.SDL_EventType.SDL_JOYDEVICEADDED:
                        break;
                    case SDL.SDL_EventType.SDL_JOYDEVICEREMOVED:
                        break;
                    case SDL.SDL_EventType.SDL_CONTROLLERAXISMOTION:
                        break;
                    case SDL.SDL_EventType.SDL_CONTROLLERBUTTONDOWN:
                        break;
                    case SDL.SDL_EventType.SDL_CONTROLLERBUTTONUP:
                        break;
                    case SDL.SDL_EventType.SDL_CONTROLLERDEVICEADDED:
                        break;
                    case SDL.SDL_EventType.SDL_CONTROLLERDEVICEREMOVED:
                        break;
                    case SDL.SDL_EventType.SDL_CONTROLLERDEVICEREMAPPED:
                        break;
                    case SDL.SDL_EventType.SDL_FINGERDOWN:
                        break;
                    case SDL.SDL_EventType.SDL_FINGERUP:
                        break;
                    case SDL.SDL_EventType.SDL_FINGERMOTION:
                        break;
                    case SDL.SDL_EventType.SDL_DOLLARGESTURE:
                        break;
                    case SDL.SDL_EventType.SDL_DOLLARRECORD:
                        break;
                    case SDL.SDL_EventType.SDL_MULTIGESTURE:
                        break;
                    case SDL.SDL_EventType.SDL_CLIPBOARDUPDATE:
                        break;
                    case SDL.SDL_EventType.SDL_DROPFILE:
                        break;
                    case SDL.SDL_EventType.SDL_DROPTEXT:
                        break;
                    case SDL.SDL_EventType.SDL_DROPBEGIN:
                        break;
                    case SDL.SDL_EventType.SDL_DROPCOMPLETE:
                        break;
                    case SDL.SDL_EventType.SDL_AUDIODEVICEADDED:
                        break;
                    case SDL.SDL_EventType.SDL_AUDIODEVICEREMOVED:
                        break;
                    case SDL.SDL_EventType.SDL_SENSORUPDATE:
                        break;
                    case SDL.SDL_EventType.SDL_RENDER_TARGETS_RESET:
                        break;
                    case SDL.SDL_EventType.SDL_RENDER_DEVICE_RESET:
                        break;
                    case SDL.SDL_EventType.SDL_USEREVENT:
                        break;
                    case SDL.SDL_EventType.SDL_LASTEVENT:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var mouseState = SDL.SDL_GetMouseState(out var mouseX, out var mouseY);
                io.MouseDown[0] = (mouseState & SDL.SDL_BUTTON(SDL.SDL_BUTTON_LEFT)) != 0;
                io.MouseDown[1] = (mouseState & SDL.SDL_BUTTON(SDL.SDL_BUTTON_RIGHT)) != 0;
                io.MouseDown[2] = (mouseState & SDL.SDL_BUTTON(SDL.SDL_BUTTON_MIDDLE)) != 0;
                SDL.SDL_GetWindowPosition(windowhandle, out var wx, out var wy);
                SDL.SDL_GetGlobalMouseState(out mouseX, out mouseY);
                mouseX -= wx;
                mouseY -= wy;
                io.MousePosPrev = io.MousePos;
                io.MousePos = new Vector2(mouseX, mouseY);
                var cursorId = ImGui.GetMouseCursor();
                if (cursorId == ImGuiMouseCursor.None)
                {
                    SDL.SDL_ShowCursor(0);
                }
                else
                {
                    SDL.SDL_SetCursor(cursors[(int)cursorId]);
                    SDL.SDL_ShowCursor(1);
                }
            }
        }

        private static class NativeMethods
        {
            [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
            public static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);
        }
    }
}
