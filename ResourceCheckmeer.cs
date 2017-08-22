// Resource Checker
// (c) 2012 Simon Oliver / HandCircus / hello@handcircus.com
// (c) 2015 Brice Clocher / Mangatome / hello@mangatome.net
// Public domain, do with whatever you like, commercial or not
// This comes with no warranty, use at your own risk!
// https://github.com/handcircus/Unity-Resource-Checker

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using Object = UnityEngine.Object;

public class TextureDetails : IEquatable<TextureDetails>
{
    public bool isCubeMap;
    public int memSizeKB;
    public Texture texture;
    public TextureFormat format;
    public int mipMapCount;
    public List<Object> FoundInMaterials = new List<Object>();
    public List<Object> FoundInRenderers = new List<Object>();
    public List<Object> FoundInAnimators = new List<Object>();
    public List<Object> FoundInScripts = new List<Object>();
    public List<Object> FoundInGraphics = new List<Object>();
    public int GOCount=0;
    public int MatCount=0;
    public bool isSky;
    public bool instance;
    public bool isgui;
    public bool npot;
    public bool isReadWriteEnable;
    public bool issRGB = false;
    public TextureWrapMode wrapmode;
    public TextureImporterCompression compress;
    public TextureDetails()
    {

    }

    public bool Equals(TextureDetails other)
    {
        return texture != null && other.texture != null &&
            texture.GetNativeTexturePtr() == other.texture.GetNativeTexturePtr();
    }

    public override int GetHashCode()
    {
        return (int)texture.GetNativeTexturePtr();
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as TextureDetails);
    }
};

public class MaterialDetails
{

    public Material material;

    public List<Renderer> FoundInRenderers = new List<Renderer>();
    public List<Graphic> FoundInGraphics = new List<Graphic>();
    public bool instance;
    public bool isgui;
    public bool isSky;

    public MaterialDetails()
    {
        instance = false;
        isgui = false;
        isSky = false;
    }
};

public class MeshDetails
{

    public Mesh mesh;

    public List<MeshFilter> FoundInMeshFilters = new List<MeshFilter>();
    public List<SkinnedMeshRenderer> FoundInSkinnedMeshRenderer = new List<SkinnedMeshRenderer>();
    public bool instance;

    public MeshDetails()
    {
        instance = false;
    }
};

public class MissingGraphic
{
    public Transform Object;
    public string type;
    public string name;
}

public class ResourceChecker : EditorWindow
{


    string[] inspectToolbarStrings = { "Textures", "Materials", "Meshes" };
    string[] inspectToolbarStrings2 = { "Textures", "Materials", "Meshes", "Missing" };
    bool[] texture_sort = { false, false, false, false, false, false, false, false, false,false };
    enum InspectType
    {
        Textures, Materials, Meshes, Missing
    };

    bool IncludeDisabledObjects = true;
    bool IncludeSpriteAnimations = true;
    bool IncludeScriptReferences = true;
    bool IncludeGuiElements = true;
    bool thingsMissing = false;

    InspectType ActiveInspectType = InspectType.Textures;

    float ThumbnailWidth = 40;
    float ThumbnailHeight = 40;

    List<TextureDetails> ActiveTextures = new List<TextureDetails>();
    List<MaterialDetails> ActiveMaterials = new List<MaterialDetails>();
    List<MeshDetails> ActiveMeshDetails = new List<MeshDetails>();
    List<MissingGraphic> MissingObjects = new List<MissingGraphic>();
    List<FileInfo> lst = new List<FileInfo>();

    Vector2 textureListScrollPos = new Vector2(0, 0);
    Vector2 materialListScrollPos = new Vector2(0, 0);
    Vector2 meshListScrollPos = new Vector2(0, 0);
    Vector2 missingListScrollPos = new Vector2(0, 0);

    int TotalTextureMemory = 0;
    int TotalMeshVertices = 0;

    bool ctrlPressed = false;

    static int MinWidth = 475;
    Color defColor;

    bool collectedInPlayingMode;
    private GUIStyle TextFieldRoundEdge;
    private GUIStyle TextFieldRoundEdgeCancelButton;
    private GUIStyle TextFieldRoundEdgeCancelButtonEmpty;
    private GUIStyle TransparentTextField;
    private string m_InputSearchText;
    private string search;
    private bool issearch = false;
    [MenuItem("Window/Resource Checker")]
    static void Init()
    {
        ResourceChecker window = (ResourceChecker)EditorWindow.GetWindow(typeof(ResourceChecker));
        window.CheckResources();
        window.minSize = new Vector2(MinWidth, 475);
        //string[] guids = AssetDatabase.FindAssets("t:scene", new string[] { "Assets" });
        //Debug.Log("end find");
        //从GUID获得资源所在路径
       // List<string> paths = new List<string>();
        //guids.ToList().ForEach(m => paths.Add(AssetDatabase.GUIDToAssetPath(m)));
        //从路径获得该资源
        //paths.ForEach(p =>allScenes.Add(AssetDatabase.LoadAssetAtPath(p, typeof(SceneAsset)) as SceneAsset));
        //下面就可以对该资源做任何你想要的操作了，如查找已丢失的脚本、检查赋值命名等，这里查找所有的Text组件个数
        
        //Debug.Log("Text count:" + allScenes.Count);
    }
    void GetGOCount(List<TextureDetails> ActiveTextures)
    {
        foreach(TextureDetails curdetail in ActiveTextures)
        {
            curdetail.GOCount = curdetail.FoundInAnimators.Count + curdetail.FoundInGraphics.Count 
                + curdetail.FoundInRenderers.Count + curdetail.FoundInScripts.Count;
            curdetail.MatCount = curdetail.FoundInMaterials.Count;
        }
    }
    void SetOthersFalse(bool[] texture_sort, int index)
    {
        for(int i = 0; i < texture_sort.Length; i++)
        {
            if (i == index) continue;
            texture_sort[i] = false;
        }
    }

    private void DrawInputTextField()
    {
        if (TextFieldRoundEdge == null)
        {
            TextFieldRoundEdge = new GUIStyle("SearchTextField");
            TextFieldRoundEdgeCancelButton = new GUIStyle("SearchCancelButton");
            TextFieldRoundEdgeCancelButtonEmpty = new GUIStyle("SearchCancelButtonEmpty");
            TransparentTextField = new GUIStyle(EditorStyles.whiteLabel);
            TransparentTextField.normal.textColor = EditorStyles.textField.normal.textColor;
        }

        //获取当前输入框的Rect(位置大小)
        Rect position = EditorGUILayout.GetControlRect();
        //设置圆角style的GUIStyle
        GUIStyle textFieldRoundEdge = TextFieldRoundEdge;
        //设置输入框的GUIStyle为透明，所以看到的“输入框”是TextFieldRoundEdge的风格
        GUIStyle transparentTextField = TransparentTextField;
        //选择取消按钮(x)的GUIStyle
        GUIStyle gUIStyle = (m_InputSearchText != "") ? TextFieldRoundEdgeCancelButton : TextFieldRoundEdgeCancelButtonEmpty;

        //输入框的水平位置向左移动取消按钮宽度的距离
        position.width -= gUIStyle.fixedWidth;
        //如果面板重绘
        if (Event.current.type == EventType.Repaint)
        {
            //根据是否是专业版来选取颜色
            GUI.contentColor = (EditorGUIUtility.isProSkin ? Color.black : new Color(0f, 0f, 0f, 0.5f));
            //当没有输入的时候提示“please Input”
            if (string.IsNullOrEmpty(m_InputSearchText))
            {
                textFieldRoundEdge.Draw(position, new GUIContent(""), 0);
            }
            else
            {
                textFieldRoundEdge.Draw(position, new GUIContent(""), 0);
            }
            //因为是“全局变量”，用完要重置回来
            GUI.contentColor = Color.white;
        }
        Rect rect = position;
        //为了空出左边那个放大镜的位置
        float num = textFieldRoundEdge.CalcSize(new GUIContent("")).x - 2f;
        rect.width -= num;
        rect.x += num;
        rect.y += 1f;//为了和后面的style对其

        m_InputSearchText = EditorGUI.TextField(rect, m_InputSearchText, transparentTextField);
        //绘制取消按钮，位置要在输入框右边
        position.x += position.width;
        position.width = gUIStyle.fixedWidth;
        position.height = gUIStyle.fixedHeight;
        if (GUI.Button(position, GUIContent.none, gUIStyle) && m_InputSearchText != "")
        {
            m_InputSearchText = "";
            //用户是否做了输入
            GUI.changed = true;
            //把焦点移开输入框
            GUIUtility.keyboardControl = 0;
        }
    }
    bool isfindalltexture = false;
    void getdir(string path, string extName)
    {
        try
        {
            string[] dir = Directory.GetDirectories(path); //文件夹列表  
            DirectoryInfo fdir = new DirectoryInfo(path);
            FileInfo[] file = fdir.GetFiles();
            //FileInfo[] file = Directory.GetFiles(path); //文件列表   
            if (file.Length != 0 || dir.Length != 0) //当前目录文件或文件夹不为空                   
            {
                foreach (FileInfo f in file) //显示当前目录所有文件   
                {
                    if (extName.ToLower().IndexOf(f.Extension.ToLower()) >= 0)
                    {
                        lst.Add(f);
                    }
                }
                foreach (string d in dir)
                {
                    getdir(d, extName);//递归   
                }
            }
        }
        catch {
        };
    }
    void OnGUI()
    {
       /* if (!isfindalltexture) {
            Debug.Log("Begin");
            isfindalltexture = true;
            getdir(Environment.CurrentDirectory, ".tga");
        }
        Debug.Log("lst count:"+lst.Count);
        Debug.Log(lst[100]);*/
        DrawInputTextField();
        defColor = GUI.color;
        IncludeDisabledObjects = GUILayout.Toggle(IncludeDisabledObjects, "Include disabled objects", GUILayout.Width(300));
        IncludeSpriteAnimations = GUILayout.Toggle(IncludeSpriteAnimations, "Look in sprite animations", GUILayout.Width(300));
       
        GUI.color = new Color(0.8f, 0.8f, 1.0f, 1.0f);
        IncludeScriptReferences = GUILayout.Toggle(IncludeScriptReferences, "Look in behavior fields", GUILayout.Width(300));
        GUI.color = new Color(1.0f, 0.95f, 0.8f, 1.0f);
        IncludeGuiElements = GUILayout.Toggle(IncludeGuiElements, "Look in GUI elements", GUILayout.Width(300));
        GUI.color = defColor;
        GUILayout.BeginArea(new Rect(position.width - 85, 25, 100, 65));
        if (GUILayout.Button("Calculate", GUILayout.Width(80), GUILayout.Height(40)))
            CheckResources();
        if (GUILayout.Button("CleanUp", GUILayout.Width(80), GUILayout.Height(20)))
            Resources.UnloadUnusedAssets();
        GUILayout.EndArea();
        RemoveDestroyedResources();
        GUILayout.Space(30);
        if (thingsMissing == true)
        {
            EditorGUI.HelpBox(new Rect(8, 75, 300, 25), "Some GameObjects are missing graphical elements.", MessageType.Error);
        }
        GUILayout.BeginHorizontal();
        Debug.Log("ActiveTextures.Count:"+ActiveTextures.Count);
        GUILayout.Label("Textures " + ActiveTextures.Count + " - " + FormatSizeString(TotalTextureMemory));
        GUILayout.Label("Materials " + ActiveMaterials.Count);
        GUILayout.Label("Meshes " + ActiveMeshDetails.Count + " - " + TotalMeshVertices + " verts");
        GUILayout.EndHorizontal();
        if (thingsMissing == true)
        {
            ActiveInspectType = (InspectType)GUILayout.Toolbar((int)ActiveInspectType, inspectToolbarStrings2);
        }
        else
        {
            ActiveInspectType = (InspectType)GUILayout.Toolbar((int)ActiveInspectType, inspectToolbarStrings);
        }

        ctrlPressed = Event.current.control || Event.current.command;
        GetGOCount(ActiveTextures);
        GUILayout.BeginHorizontal();
        GUILayout.Space(500);
        bool descending = GUILayout.Button("sort by descending", GUILayout.Width(160), GUILayout.Height(20)),
            ascending = GUILayout.Button("sort by ascending", GUILayout.Width(160), GUILayout.Height(20));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Thumbnail");
        GUILayout.Space(22);
        GUILayout.Label("Name");
        GUILayout.Space(60);
        texture_sort[0] = GUILayout.Toggle(texture_sort[0], "Size");
        if(texture_sort[0])
            SetOthersFalse(texture_sort, 0);
        GUILayout.Space(2);
        texture_sort[1] = GUILayout.Toggle(texture_sort[1], "Resolution");
        if (texture_sort[1])
            SetOthersFalse(texture_sort, 1);
        GUILayout.Space(2);
        texture_sort[2] = GUILayout.Toggle(texture_sort[2], "npot");
        if (texture_sort[2])
            SetOthersFalse(texture_sort, 2);
        GUILayout.Space(2);
        texture_sort[9] = GUILayout.Toggle(texture_sort[9], "Compress");
        if (texture_sort[9])
            SetOthersFalse(texture_sort, 9);
        GUILayout.Space(2);
        texture_sort[3] = GUILayout.Toggle(texture_sort[3], "Read/Write enable");
        if (texture_sort[3])
            SetOthersFalse(texture_sort, 3);
        GUILayout.Space(2);
        texture_sort[4] = GUILayout.Toggle(texture_sort[4], "Mipmap");
        if (texture_sort[4])
            SetOthersFalse(texture_sort, 4);
        GUILayout.Space(2);
        texture_sort[5] = GUILayout.Toggle(texture_sort[5], "sRGB");
        if (texture_sort[5])
            SetOthersFalse(texture_sort, 5);
        GUILayout.Space(2);
        texture_sort[6] = GUILayout.Toggle(texture_sort[6], "Wrap Mode");
        if (texture_sort[6])
            SetOthersFalse(texture_sort, 6);
        GUILayout.Space(2);
        texture_sort[7] = GUILayout.Toggle(texture_sort[7], "Material Count");
        if (texture_sort[7])
            SetOthersFalse(texture_sort, 7);
        GUILayout.Space(2);
        texture_sort[8] = GUILayout.Toggle(texture_sort[8], "GO Count");
        if (texture_sort[8])
            SetOthersFalse(texture_sort, 8);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Space(400);
        GUILayout.Label("Change the NPOT\n into POT");
        GUILayout.Label("Mask shouldn't\n be sRGB");
        GUI.color = new Color(0.8f, 0.0f, 0.0f, 1.0f);

        GUILayout.EndHorizontal();
        GUI.color = defColor;
        if (texture_sort[0])
        {
            if (descending || ascending)
            {
                List<TextureDetails> SortedList;
                if (descending)
                    SortedList = ActiveTextures.OrderByDescending(o => o.memSizeKB).ToList();
                else
                    SortedList = ActiveTextures.OrderBy(o => o.memSizeKB).ToList();
                ActiveTextures.Clear();
                ActiveTextures = new List<TextureDetails>(SortedList.ToArray()); // copy
            }
        }
        if (texture_sort[1])
        {
            if (descending || ascending)
            {
                List<TextureDetails> SortedList;
                if (descending)
                    SortedList = ActiveTextures.OrderByDescending(o => (o.texture.width * o.texture.height)).ThenBy(o => o.texture.width).ToList();
                else
                    SortedList = ActiveTextures.OrderBy(o => o.texture.width * o.texture.height).ThenBy(o => o.texture.width).ToList();
                ActiveTextures.Clear();
                ActiveTextures = new List<TextureDetails>(SortedList.ToArray()); // copy
            }
        }
        else if(texture_sort[2])
        {
            if (descending || ascending)
            {
                List<TextureDetails> SortedList;
                if (descending)
                    SortedList = ActiveTextures.OrderByDescending(o => o.npot).ToList();
                else
                    SortedList = ActiveTextures.OrderBy(o => o.npot).ToList();
                ActiveTextures.Clear();
                ActiveTextures = new List<TextureDetails>(SortedList.ToArray()); // copy
            }
        }
        else if (texture_sort[3])
        {
            if (descending || ascending)
            {
                List<TextureDetails> SortedList;
                if (descending)
                    SortedList = ActiveTextures.OrderByDescending(o => o.isReadWriteEnable).ToList();
                else
                    SortedList = ActiveTextures.OrderBy(o => o.isReadWriteEnable).ToList();
                ActiveTextures.Clear();
                ActiveTextures = new List<TextureDetails>(SortedList.ToArray()); // copy
            }
        }
         else if (texture_sort[4])
        {
            if (descending || ascending)
            {
                List<TextureDetails> SortedList;
                if (descending)
                    SortedList = ActiveTextures.OrderByDescending(o => o.mipMapCount).ToList();
                else
                    SortedList = ActiveTextures.OrderBy(o => o.mipMapCount).ToList();
                ActiveTextures.Clear();
                ActiveTextures = new List<TextureDetails>(SortedList.ToArray()); // copy
            }
        }
        else if(texture_sort[5])
        {
            if (descending || ascending)
            {
                List<TextureDetails> SortedList;
                if (descending)
                    SortedList = ActiveTextures.OrderByDescending(o => o.issRGB).ToList();
                else
                    SortedList = ActiveTextures.OrderBy(o => o.issRGB).ToList();
                ActiveTextures.Clear();
                ActiveTextures = new List<TextureDetails>(SortedList.ToArray()); // copy
            }
        }
        else if(texture_sort[6])
        {
            if (descending || ascending)
            {
                List<TextureDetails> SortedList;
                if (descending)
                    SortedList = ActiveTextures.OrderByDescending(o => o.wrapmode).ToList();
                else
                    SortedList = ActiveTextures.OrderBy(o => o.wrapmode).ToList();
                ActiveTextures.Clear();
                ActiveTextures = new List<TextureDetails>(SortedList.ToArray()); // copy
            }
        }
        else if(texture_sort[8])
        {
            if (descending || ascending)
            {
                List<TextureDetails> SortedList;
                if (descending)
                    SortedList = ActiveTextures.OrderByDescending(o => o.GOCount).ToList();
                else
                    SortedList = ActiveTextures.OrderBy(o => o.GOCount).ToList();
                ActiveTextures.Clear();
                ActiveTextures = new List<TextureDetails>(SortedList.ToArray()); // copy
            }

        }else if (texture_sort[7])
        {
            if (descending || ascending)
            {
                List<TextureDetails> SortedList;
                if (descending)
                    SortedList = ActiveTextures.OrderByDescending(o => o.MatCount).ToList();
                else 
                    SortedList = ActiveTextures.OrderBy(o => o.MatCount).ToList();
                ActiveTextures.Clear();
                ActiveTextures = new List<TextureDetails>(SortedList.ToArray()); // copy
            }
        }
        else if (texture_sort[9])
        {
            if (descending || ascending)
            {
                List<TextureDetails> SortedList;
                if (descending)
                    SortedList = ActiveTextures.OrderByDescending(o => o.compress).ToList();
                else
                    SortedList = ActiveTextures.OrderBy(o => o.compress).ToList();
                ActiveTextures.Clear();
                ActiveTextures = new List<TextureDetails>(SortedList.ToArray()); // copy
            }
        }

        
        switch (ActiveInspectType)
        {
            case InspectType.Textures:
                ListTextures();
                break;
            case InspectType.Materials:
                ListMaterials();
                break;
            case InspectType.Meshes:
                ListMeshes();
                break;
            case InspectType.Missing:
                ListMissing();
                break;
        }
    }

    private void RemoveDestroyedResources()
    {
        if (collectedInPlayingMode != Application.isPlaying)
        {
            ActiveTextures.Clear();
            ActiveMaterials.Clear();
            ActiveMeshDetails.Clear();
            MissingObjects.Clear();
            thingsMissing = false;
            collectedInPlayingMode = Application.isPlaying;
        }

        ActiveTextures.RemoveAll(x => !x.texture);
        ActiveTextures.ForEach(delegate (TextureDetails obj) {
            obj.FoundInAnimators.RemoveAll(x => !x);
            obj.FoundInMaterials.RemoveAll(x => !x);
            obj.FoundInRenderers.RemoveAll(x => !x);
            obj.FoundInScripts.RemoveAll(x => !x);
            obj.FoundInGraphics.RemoveAll(x => !x);
        });

        ActiveMaterials.RemoveAll(x => !x.material);
        ActiveMaterials.ForEach(delegate (MaterialDetails obj) {
            obj.FoundInRenderers.RemoveAll(x => !x);
            obj.FoundInGraphics.RemoveAll(x => !x);
        });

        ActiveMeshDetails.RemoveAll(x => !x.mesh);
        ActiveMeshDetails.ForEach(delegate (MeshDetails obj) {
            obj.FoundInMeshFilters.RemoveAll(x => !x);
            obj.FoundInSkinnedMeshRenderer.RemoveAll(x => !x);
        });

        TotalTextureMemory = 0;
        foreach (TextureDetails tTextureDetails in ActiveTextures) TotalTextureMemory += tTextureDetails.memSizeKB;

        TotalMeshVertices = 0;
        foreach (MeshDetails tMeshDetails in ActiveMeshDetails) TotalMeshVertices += tMeshDetails.mesh.vertexCount;
    }

    int GetBitsPerPixel(TextureFormat format)
    {
        switch (format)
        {
            case TextureFormat.Alpha8: //	 Alpha-only texture format.
                return 8;
            case TextureFormat.ARGB4444: //	 A 16 bits/pixel texture format. Texture stores color with an alpha channel.
                return 16;
            case TextureFormat.RGBA4444: //	 A 16 bits/pixel texture format.
                return 16;
            case TextureFormat.RGB24:   // A color texture format.
                return 24;
            case TextureFormat.RGBA32:  //Color with an alpha channel texture format.
                return 32;
            case TextureFormat.ARGB32:  //Color with an alpha channel texture format.
                return 32;
            case TextureFormat.RGB565:  //	 A 16 bit color texture format.
                return 16;
            case TextureFormat.DXT1:    // Compressed color texture format.
                return 4;
            case TextureFormat.DXT5:    // Compressed color with alpha channel texture format.
                return 8;
            /*
			case TextureFormat.WiiI4:	// Wii texture format.
			case TextureFormat.WiiI8:	// Wii texture format. Intensity 8 bit.
			case TextureFormat.WiiIA4:	// Wii texture format. Intensity + Alpha 8 bit (4 + 4).
			case TextureFormat.WiiIA8:	// Wii texture format. Intensity + Alpha 16 bit (8 + 8).
			case TextureFormat.WiiRGB565:	// Wii texture format. RGB 16 bit (565).
			case TextureFormat.WiiRGB5A3:	// Wii texture format. RGBA 16 bit (4443).
			case TextureFormat.WiiRGBA8:	// Wii texture format. RGBA 32 bit (8888).
			case TextureFormat.WiiCMPR:	//	 Compressed Wii texture format. 4 bits/texel, ~RGB8A1 (Outline alpha is not currently supported).
				return 0;  //Not supported yet
			*/
            case TextureFormat.PVRTC_RGB2://	 PowerVR (iOS) 2 bits/pixel compressed color texture format.
                return 2;
            case TextureFormat.PVRTC_RGBA2://	 PowerVR (iOS) 2 bits/pixel compressed with alpha channel texture format
                return 2;
            case TextureFormat.PVRTC_RGB4://	 PowerVR (iOS) 4 bits/pixel compressed color texture format.
                return 4;
            case TextureFormat.PVRTC_RGBA4://	 PowerVR (iOS) 4 bits/pixel compressed with alpha channel texture format
                return 4;
            case TextureFormat.ETC_RGB4://	 ETC (GLES2.0) 4 bits/pixel compressed RGB texture format.
                return 4;
            case TextureFormat.ATC_RGB4://	 ATC (ATITC) 4 bits/pixel compressed RGB texture format.
                return 4;
            case TextureFormat.ATC_RGBA8://	 ATC (ATITC) 8 bits/pixel compressed RGB texture format.
                return 8;
            case TextureFormat.BGRA32://	 Format returned by iPhone camera
                return 32;
#if !UNITY_5
			case TextureFormat.ATF_RGB_DXT1://	 Flash-specific RGB DXT1 compressed color texture format.
			case TextureFormat.ATF_RGBA_JPG://	 Flash-specific RGBA JPG-compressed color texture format.
			case TextureFormat.ATF_RGB_JPG://	 Flash-specific RGB JPG-compressed color texture format.
			return 0; //Not supported yet  
#endif
        }
        return 0;
    }

    int CalculateTextureSizeBytes(Texture tTexture)
    {

        int tWidth = tTexture.width;
        int tHeight = tTexture.height;
        if (tTexture is Texture2D)
        {
            Texture2D tTex2D = tTexture as Texture2D;
            int bitsPerPixel = GetBitsPerPixel(tTex2D.format);
            int mipMapCount = tTex2D.mipmapCount;
            int mipLevel = 1;
            int tSize = 0;
            while (mipLevel <= mipMapCount)
            {
                tSize += tWidth * tHeight * bitsPerPixel / 8;
                tWidth = tWidth / 2;
                tHeight = tHeight / 2;
                mipLevel++;
            }
            return tSize;
        }
        if (tTexture is Texture2DArray)
        {
            Texture2DArray tTex2D = tTexture as Texture2DArray;
            int bitsPerPixel = GetBitsPerPixel(tTex2D.format);
            int mipMapCount = 10;
            int mipLevel = 1;
            int tSize = 0;
            while (mipLevel <= mipMapCount)
            {
                tSize += tWidth * tHeight * bitsPerPixel / 8;
                tWidth = tWidth / 2;
                tHeight = tHeight / 2;
                mipLevel++;
            }
            return tSize * ((Texture2DArray)tTex2D).depth;
        }
        if (tTexture is Cubemap)
        {
            Cubemap tCubemap = tTexture as Cubemap;
            int bitsPerPixel = GetBitsPerPixel(tCubemap.format);
            return tWidth * tHeight * 6 * bitsPerPixel / 8;
        }
        return 0;
    }


    void SelectObject(Object selectedObject, bool append)
    {
        if (append)
        {
            List<Object> currentSelection = new List<Object>(Selection.objects);
            // Allow toggle selection
            if (currentSelection.Contains(selectedObject)) currentSelection.Remove(selectedObject);
            else currentSelection.Add(selectedObject);

            Selection.objects = currentSelection.ToArray();
        }
        else Selection.activeObject = selectedObject;
    }

    void SelectObjects(List<Object> selectedObjects, bool append)
    {
        if (append)
        {
            List<Object> currentSelection = new List<Object>(Selection.objects);
            currentSelection.AddRange(selectedObjects);
            Selection.objects = currentSelection.ToArray();
        }
        else Selection.objects = selectedObjects.ToArray();
    }

    void ListTextures()
    {
        textureListScrollPos = EditorGUILayout.BeginScrollView(textureListScrollPos);

        foreach (TextureDetails tDetails in ActiveTextures)
        {
            Texture tex = new Texture();
            tex = tDetails.texture;
            if (m_InputSearchText!=null && !tex.name.Contains(m_InputSearchText))
                continue;
            GUILayout.BeginHorizontal();
            // tex.GetType().GetProperty("SRGB")
            GUILayout.Space(15);
            if (tDetails.texture.GetType() == typeof(Texture2DArray) || tDetails.texture.GetType() == typeof(Cubemap))
            {
                tex = AssetPreview.GetMiniThumbnail(tDetails.texture);
            }
            GUILayout.Box(tex, GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

            if (tDetails.instance == true)
                GUI.color = new Color(0.8f, 0.8f, defColor.b, 1.0f);
            if (tDetails.isgui == true)
                GUI.color = new Color(defColor.r, 0.95f, 0.8f, 1.0f);
            if (tDetails.isSky)
                GUI.color = new Color(0.9f, defColor.g, defColor.b, 1.0f);
            GUILayout.Space(15);
            if (GUILayout.Button(tDetails.texture.name, GUILayout.Width(150)))
            {
                SelectObject(tDetails.texture, ctrlPressed);
            }
            GUI.color = defColor;
            GUILayout.Space(35);
            GUILayout.Label(FormatSizeString(tDetails.memSizeKB), GUILayout.Width(50));
            GUILayout.Space(35);
            GUILayout.Label(tDetails.texture.width + "x" + tDetails.texture.height, GUILayout.Width(70));
            GUILayout.Space(40);
            if (tDetails.npot)
                GUILayout.Label("No", GUILayout.Width(30));
            else
                GUILayout.Label("Yes", GUILayout.Width(30));
            GUILayout.Space(20);
            GUILayout.Label(tDetails.compress+"", GUILayout.Width(100));
            GUILayout.Space(60);
            if (tDetails.isReadWriteEnable)
                GUILayout.Label("Yes", GUILayout.Width(30));
            else
                GUILayout.Label("No", GUILayout.Width(30));
            GUILayout.Space(90);
            GUILayout.Label(tDetails.mipMapCount + "mip", GUILayout.Width(50));
            GUILayout.Space(40);
            if (tDetails.issRGB)
                GUILayout.Label("Yes", GUILayout.Width(30));
            else
                GUILayout.Label("No", GUILayout.Width(30));
            GUILayout.Space(60);
            GUILayout.Label(tDetails.wrapmode+"", GUILayout.Width(70));
            GUILayout.Space(35);
            if (GUILayout.Button(tDetails.MatCount + " Mat", GUILayout.Width(50)))
            {
                SelectObjects(tDetails.FoundInMaterials, ctrlPressed);
            }
            GUILayout.Space(70);
            HashSet<Object> FoundObjects = new HashSet<Object>();
            foreach (Renderer renderer in tDetails.FoundInRenderers) FoundObjects.Add(renderer.gameObject);
            foreach (Animator animator in tDetails.FoundInAnimators) FoundObjects.Add(animator.gameObject);
            foreach (Graphic graphic in tDetails.FoundInGraphics) FoundObjects.Add(graphic.gameObject);
            foreach (MonoBehaviour script in tDetails.FoundInScripts) FoundObjects.Add(script.gameObject);
            if (GUILayout.Button(tDetails.GOCount + " GO", GUILayout.Width(50)))
            {
                SelectObjects(new List<Object>(FoundObjects), ctrlPressed);
            }

            GUILayout.EndHorizontal();
        }
        if (ActiveTextures.Count > 0)
        {
            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            //GUILayout.Box(" ",GUILayout.Width(ThumbnailWidth),GUILayout.Height(ThumbnailHeight));
            if (GUILayout.Button("Select \n All", GUILayout.Width(ThumbnailWidth * 2)))
            {
                List<Object> AllTextures = new List<Object>();
                foreach (TextureDetails tDetails in ActiveTextures) AllTextures.Add(tDetails.texture);
                SelectObjects(AllTextures, ctrlPressed);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    void ListMaterials()
    {
        materialListScrollPos = EditorGUILayout.BeginScrollView(materialListScrollPos);

        foreach (MaterialDetails tDetails in ActiveMaterials)
        {
            if (tDetails.material != null)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Box(AssetPreview.GetAssetPreview(tDetails.material), GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

                if (tDetails.instance == true)
                    GUI.color = new Color(0.8f, 0.8f, defColor.b, 1.0f);
                if (tDetails.isgui == true)
                    GUI.color = new Color(defColor.r, 0.95f, 0.8f, 1.0f);
                if (tDetails.isSky)
                    GUI.color = new Color(0.9f, defColor.g, defColor.b, 1.0f);
                if (GUILayout.Button(tDetails.material.name, GUILayout.Width(150)))
                {
                    SelectObject(tDetails.material, ctrlPressed);
                }
                GUI.color = defColor;

                string shaderLabel = tDetails.material.shader != null ? tDetails.material.shader.name : "no shader";
                GUILayout.Label(shaderLabel, GUILayout.Width(200));

                if (GUILayout.Button((tDetails.FoundInRenderers.Count + tDetails.FoundInGraphics.Count) + " GO", GUILayout.Width(50)))
                {
                    List<Object> FoundObjects = new List<Object>();
                    foreach (Renderer renderer in tDetails.FoundInRenderers) FoundObjects.Add(renderer.gameObject);
                    foreach (Graphic graphic in tDetails.FoundInGraphics) FoundObjects.Add(graphic.gameObject);
                    SelectObjects(FoundObjects, ctrlPressed);
                }


                GUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndScrollView();
    }

    void ListMeshes()
    {
        meshListScrollPos = EditorGUILayout.BeginScrollView(meshListScrollPos);

        foreach (MeshDetails tDetails in ActiveMeshDetails)
        {
            if (tDetails.mesh != null)
            {
                GUILayout.BeginHorizontal();
                string name = tDetails.mesh.name;
                if (name == null || name.Count() < 1)
                    name = tDetails.FoundInMeshFilters[0].gameObject.name;
                if (tDetails.instance == true)
                    GUI.color = new Color(0.8f, 0.8f, defColor.b, 1.0f);
                if (GUILayout.Button(name, GUILayout.Width(150)))
                {
                    SelectObject(tDetails.mesh, ctrlPressed);
                }
                GUI.color = defColor;
                string sizeLabel = "" + tDetails.mesh.vertexCount + " vert";

                GUILayout.Label(sizeLabel, GUILayout.Width(100));


                if (GUILayout.Button(tDetails.FoundInMeshFilters.Count + " GO", GUILayout.Width(50)))
                {
                    List<Object> FoundObjects = new List<Object>();
                    foreach (MeshFilter meshFilter in tDetails.FoundInMeshFilters) FoundObjects.Add(meshFilter.gameObject);
                    SelectObjects(FoundObjects, ctrlPressed);
                }
                if (tDetails.FoundInSkinnedMeshRenderer.Count > 0)
                {
                    if (GUILayout.Button(tDetails.FoundInSkinnedMeshRenderer.Count + " skinned mesh GO", GUILayout.Width(140)))
                    {
                        List<Object> FoundObjects = new List<Object>();
                        foreach (SkinnedMeshRenderer skinnedMeshRenderer in tDetails.FoundInSkinnedMeshRenderer)
                            FoundObjects.Add(skinnedMeshRenderer.gameObject);
                        SelectObjects(FoundObjects, ctrlPressed);
                    }
                }
                else
                {
                    GUI.color = new Color(defColor.r, defColor.g, defColor.b, 0.5f);
                    GUILayout.Label("   0 skinned mesh");
                    GUI.color = defColor;
                }


                GUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndScrollView();
    }

    void ListMissing()
    {
        missingListScrollPos = EditorGUILayout.BeginScrollView(missingListScrollPos);
        foreach (MissingGraphic dMissing in MissingObjects)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(dMissing.name, GUILayout.Width(150)))
                SelectObject(dMissing.Object, ctrlPressed);
            GUILayout.Label("missing ", GUILayout.Width(48));
            switch (dMissing.type)
            {
                case "mesh":
                    GUI.color = new Color(0.8f, 0.8f, defColor.b, 1.0f);
                    break;
                case "sprite":
                    GUI.color = new Color(defColor.r, 0.8f, 0.8f, 1.0f);
                    break;
                case "material":
                    GUI.color = new Color(0.8f, defColor.g, 0.8f, 1.0f);
                    break;
            }
            GUILayout.Label(dMissing.type);
            GUI.color = defColor;
            GUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    string FormatSizeString(int memSizeKB)
    {
        if (memSizeKB < 1024) return "" + memSizeKB + "k";
        else
        {
            float memSizeMB = ((float)memSizeKB) / 1024.0f;
            return memSizeMB.ToString("0.00") + "Mb";
        }
    }


    TextureDetails FindTextureDetails(Texture tTexture)
    {
        foreach (TextureDetails tTextureDetails in ActiveTextures)
        {
            if (tTextureDetails.texture == tTexture) return tTextureDetails;
        }
        return null;

    }

    MaterialDetails FindMaterialDetails(Material tMaterial)
    {
        foreach (MaterialDetails tMaterialDetails in ActiveMaterials)
        {
            if (tMaterialDetails.material == tMaterial) return tMaterialDetails;
        }
        return null;

    }

    MeshDetails FindMeshDetails(Mesh tMesh)
    {
        foreach (MeshDetails tMeshDetails in ActiveMeshDetails)
        {
            if (tMeshDetails.mesh == tMesh) return tMeshDetails;
        }
        return null;

    }


    void CheckResources()
    {
        ActiveTextures.Clear();
        ActiveMaterials.Clear();
        ActiveMeshDetails.Clear();
        MissingObjects.Clear();
        thingsMissing = false;

        Renderer[] renderers = FindObjects<Renderer>();
        //Debug.Log("meshfilters:"+ renderers.Length);
        MaterialDetails skyMat = new MaterialDetails();
        skyMat.material = RenderSettings.skybox;
        skyMat.isSky = true;
        ActiveMaterials.Add(skyMat);

        //Debug.Log("Total renderers "+renderers.Length);
        foreach (Renderer renderer in renderers)
        {
            Debug.Log("Renderer is "+ renderer.sharedMaterials.Length);
            foreach (Material material in renderer.sharedMaterials)
            {

                MaterialDetails tMaterialDetails = FindMaterialDetails(material);
                if (tMaterialDetails == null)
                {
                    tMaterialDetails = new MaterialDetails();
                    tMaterialDetails.material = material;
                    ActiveMaterials.Add(tMaterialDetails);
                }
                tMaterialDetails.FoundInRenderers.Add(renderer);
            }

            if (renderer is SpriteRenderer)
            {
                SpriteRenderer tSpriteRenderer = (SpriteRenderer)renderer;

                if (tSpriteRenderer.sprite != null)
                {
                    var tSpriteTextureDetail = GetTextureDetail(tSpriteRenderer.sprite.texture, renderer);
                    if (!ActiveTextures.Contains(tSpriteTextureDetail))
                    {
                        ActiveTextures.Add(tSpriteTextureDetail);
                    }
                }
                else if (tSpriteRenderer.sprite == null)
                {
                    MissingGraphic tMissing = new MissingGraphic();
                    tMissing.Object = tSpriteRenderer.transform;
                    tMissing.type = "sprite";
                    tMissing.name = tSpriteRenderer.transform.name;
                    MissingObjects.Add(tMissing);
                    thingsMissing = true;
                }
            }
        }

        if (IncludeGuiElements)
        {
            Graphic[] graphics = FindObjects<Graphic>();

            foreach (Graphic graphic in graphics)
            {
                if (graphic.mainTexture)
                {
                    var tSpriteTextureDetail = GetTextureDetail(graphic.mainTexture, graphic);
                    if (!ActiveTextures.Contains(tSpriteTextureDetail))
                    {
                        ActiveTextures.Add(tSpriteTextureDetail);
                    }
                }

                if (graphic.materialForRendering)
                {
                    MaterialDetails tMaterialDetails = FindMaterialDetails(graphic.materialForRendering);
                    if (tMaterialDetails == null)
                    {
                        tMaterialDetails = new MaterialDetails();
                        tMaterialDetails.material = graphic.materialForRendering;
                        tMaterialDetails.isgui = true;
                        ActiveMaterials.Add(tMaterialDetails);
                    }
                    tMaterialDetails.FoundInGraphics.Add(graphic);
                }
            }
        }

        Debug.Log(ActiveMaterials.Count);
        foreach (MaterialDetails tMaterialDetails in ActiveMaterials)
        {
            Material tMaterial = tMaterialDetails.material;
            if (tMaterial != null)
            {
                var dependencies = EditorUtility.CollectDependencies(new UnityEngine.Object[] { tMaterial });
                foreach (Object obj in dependencies)
                {
                    if (obj is Texture)
                    {
                        Texture tTexture = obj as Texture;
                        if (tTexture == null)
                            Debug.Log("tTexture");
                        else if (tMaterial == null)
                            Debug.Log("tMaterial");
                        else if(tMaterialDetails == null)
                            Debug.Log("tMaterialDetails"); 
                        var tTextureDetail = GetTextureDetail(tTexture, tMaterial, tMaterialDetails);
                        tTextureDetail.isSky = tMaterialDetails.isSky;
                        tTextureDetail.instance = tMaterialDetails.instance;
                        tTextureDetail.isgui = tMaterialDetails.isgui;
                        ActiveTextures.Add(tTextureDetail);
                    }
                }

                //if the texture was downloaded, it won't be included in the editor dependencies
                if (tMaterial.HasProperty("_MainTex"))
                {
                    if (tMaterial.mainTexture != null && !dependencies.Contains(tMaterial.mainTexture))
                    {
                        var tTextureDetail = GetTextureDetail(tMaterial.mainTexture, tMaterial, tMaterialDetails);
                        ActiveTextures.Add(tTextureDetail);
                    }
                }
            }
        }


        MeshFilter[] meshFilters = FindObjects<MeshFilter>();

        foreach (MeshFilter tMeshFilter in meshFilters)
        {
            Mesh tMesh = tMeshFilter.sharedMesh;
            if (tMesh != null)
            {
                MeshDetails tMeshDetails = FindMeshDetails(tMesh);
                if (tMeshDetails == null)
                {
                    tMeshDetails = new MeshDetails();
                    tMeshDetails.mesh = tMesh;
                    ActiveMeshDetails.Add(tMeshDetails);
                }
                tMeshDetails.FoundInMeshFilters.Add(tMeshFilter);
            }
            else if (tMesh == null && tMeshFilter.transform.GetComponent("TextContainer") == null)
            {
                MissingGraphic tMissing = new MissingGraphic();
                tMissing.Object = tMeshFilter.transform;
                tMissing.type = "mesh";
                tMissing.name = tMeshFilter.transform.name;
                MissingObjects.Add(tMissing);
                thingsMissing = true;
            }
            if (tMeshFilter.transform.GetComponent<MeshRenderer>().sharedMaterial == null)
            {
                MissingGraphic tMissing = new MissingGraphic();
                tMissing.Object = tMeshFilter.transform;
                tMissing.type = "material";
                tMissing.name = tMeshFilter.transform.name;
                MissingObjects.Add(tMissing);
                thingsMissing = true;
            }
        }

        SkinnedMeshRenderer[] skinnedMeshRenderers = FindObjects<SkinnedMeshRenderer>();

        foreach (SkinnedMeshRenderer tSkinnedMeshRenderer in skinnedMeshRenderers)
        {
            Mesh tMesh = tSkinnedMeshRenderer.sharedMesh;
            if (tMesh != null)
            {
                MeshDetails tMeshDetails = FindMeshDetails(tMesh);
                if (tMeshDetails == null)
                {
                    tMeshDetails = new MeshDetails();
                    tMeshDetails.mesh = tMesh;
                    ActiveMeshDetails.Add(tMeshDetails);
                }
                tMeshDetails.FoundInSkinnedMeshRenderer.Add(tSkinnedMeshRenderer);
            }
            else if (tMesh == null)
            {
                MissingGraphic tMissing = new MissingGraphic();
                tMissing.Object = tSkinnedMeshRenderer.transform;
                tMissing.type = "mesh";
                tMissing.name = tSkinnedMeshRenderer.transform.name;
                MissingObjects.Add(tMissing);
                thingsMissing = true;
            }
            if (tSkinnedMeshRenderer.sharedMaterial == null)
            {
                MissingGraphic tMissing = new MissingGraphic();
                tMissing.Object = tSkinnedMeshRenderer.transform;
                tMissing.type = "material";
                tMissing.name = tSkinnedMeshRenderer.transform.name;
                MissingObjects.Add(tMissing);
                thingsMissing = true;
            }
        }

        if (IncludeSpriteAnimations)
        {
            Animator[] animators = FindObjects<Animator>();
            foreach (Animator anim in animators)
            {
#if UNITY_4_6 || UNITY_4_5 || UNITY_4_4 || UNITY_4_3
				UnityEditorInternal.AnimatorController ac = anim.runtimeAnimatorController as UnityEditorInternal.AnimatorController;
#elif UNITY_5
                UnityEditor.Animations.AnimatorController ac = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
#endif

                //Skip animators without layers, this can happen if they don't have an animator controller.
                if (!ac || ac.layers == null || ac.layers.Length == 0)
                    continue;

                for (int x = 0; x < anim.layerCount; x++)
                {
#if UNITY_4_6 || UNITY_4_5 || UNITY_4_4 || UNITY_4_3
					UnityEditorInternal.StateMachine sm = ac.GetLayer(x).stateMachine;
					int cnt = sm.stateCount;
#elif UNITY_5
                    UnityEditor.Animations.AnimatorStateMachine sm = ac.layers[x].stateMachine;
                    int cnt = sm.states.Length;
#endif

                    for (int i = 0; i < cnt; i++)
                    {
#if UNITY_4_6 || UNITY_4_5 || UNITY_4_4 || UNITY_4_3
						UnityEditorInternal.State state = sm.GetState(i);
						Motion m = state.GetMotion();
#elif UNITY_5
                        UnityEditor.Animations.AnimatorState state = sm.states[i].state;
                        Motion m = state.motion;
#endif
                        if (m != null)
                        {
                            AnimationClip clip = m as AnimationClip;

                            if (clip != null)
                            {
                                EditorCurveBinding[] ecbs = AnimationUtility.GetObjectReferenceCurveBindings(clip);

                                foreach (EditorCurveBinding ecb in ecbs)
                                {
                                    if (ecb.propertyName == "m_Sprite")
                                    {
                                        foreach (ObjectReferenceKeyframe keyframe in AnimationUtility.GetObjectReferenceCurve(clip, ecb))
                                        {
                                            Sprite tSprite = keyframe.value as Sprite;

                                            if (tSprite != null)
                                            {
                                                var tTextureDetail = GetTextureDetail(tSprite.texture, anim);
                                                if (!ActiveTextures.Contains(tTextureDetail))
                                                {
                                                    ActiveTextures.Add(tTextureDetail);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

            }
        }

        if (IncludeScriptReferences)
        {
            MonoBehaviour[] scripts = FindObjects<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                BindingFlags flags = BindingFlags.Public | BindingFlags.Instance; // only public non-static fields are bound to by Unity.
                FieldInfo[] fields = script.GetType().GetFields(flags);

                foreach (FieldInfo field in fields)
                {
                    System.Type fieldType = field.FieldType;
                    if (fieldType == typeof(Sprite))
                    {
                        Sprite tSprite = field.GetValue(script) as Sprite;
                        if (tSprite != null)
                        {
                            var tSpriteTextureDetail = GetTextureDetail(tSprite.texture, script);
                            if (!ActiveTextures.Contains(tSpriteTextureDetail))
                            {
                                ActiveTextures.Add(tSpriteTextureDetail);
                            }
                        }
                    }
                    if (fieldType == typeof(Mesh))
                    {
                        Mesh tMesh = field.GetValue(script) as Mesh;
                        if (tMesh != null)
                        {
                            MeshDetails tMeshDetails = FindMeshDetails(tMesh);
                            if (tMeshDetails == null)
                            {
                                tMeshDetails = new MeshDetails();
                                tMeshDetails.mesh = tMesh;
                                tMeshDetails.instance = true;
                                ActiveMeshDetails.Add(tMeshDetails);
                            }
                        }
                    }
                    if (fieldType == typeof(Material))
                    {
                        Material tMaterial = field.GetValue(script) as Material;
                        if (tMaterial != null)
                        {
                            MaterialDetails tMatDetails = FindMaterialDetails(tMaterial);
                            if (tMatDetails == null)
                            {
                                tMatDetails = new MaterialDetails();
                                tMatDetails.instance = true;
                                tMatDetails.material = tMaterial;
                                if (!ActiveMaterials.Contains(tMatDetails))
                                    ActiveMaterials.Add(tMatDetails);
                            }
                            if (tMaterial.mainTexture)
                            {
                                var tSpriteTextureDetail = GetTextureDetail(tMaterial.mainTexture);
                                if (!ActiveTextures.Contains(tSpriteTextureDetail))
                                {
                                    ActiveTextures.Add(tSpriteTextureDetail);
                                }
                            }
                            var dependencies = EditorUtility.CollectDependencies(new UnityEngine.Object[] { tMaterial });
                            foreach (Object obj in dependencies)
                            {
                                if (obj is Texture)
                                {
                                    Texture tTexture = obj as Texture;
                                    var tTextureDetail = GetTextureDetail(tTexture, tMaterial, tMatDetails);
                                    if (!ActiveTextures.Contains(tTextureDetail))
                                        ActiveTextures.Add(tTextureDetail);
                                }
                            }
                        }
                    }
                }
            }
        }

        TotalTextureMemory = 0;
        foreach (TextureDetails tTextureDetails in ActiveTextures) TotalTextureMemory += tTextureDetails.memSizeKB;

        TotalMeshVertices = 0;
        foreach (MeshDetails tMeshDetails in ActiveMeshDetails) TotalMeshVertices += tMeshDetails.mesh.vertexCount;

        // Sort by size, descending
        //ActiveTextures.Sort(delegate (TextureDetails details1, TextureDetails details2) { return details2.memSizeKB - details1.memSizeKB; });
        //ActiveTextures = ActiveTextures.Distinct().ToList();
        //ActiveMeshDetails.Sort(delegate (MeshDetails details1, MeshDetails details2) { return details2.mesh.vertexCount - details1.mesh.vertexCount; });

        collectedInPlayingMode = Application.isPlaying;
    }

    private T[] FindObjects<T>() where T : Object
    {
        
        if (IncludeDisabledObjects)
        {
            List<T> meshfilters = new List<T>();
            List<GameObject> allGo = new List<GameObject>();
            for(int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                List<GameObject> curGO = scene.GetRootGameObjects().ToList<GameObject>();
                allGo = allGo.Concat(curGO).ToList();
                //Debug.Log("allGo count : " + allGo.Count());

            }
            //Debug.Log("sceneCount : " + UnityEngine.SceneManagement.SceneManager.sceneCount);

            foreach (GameObject go in allGo)
            {
                Transform[] tgo = go.GetComponentsInChildren<Transform>(true).ToArray();
                foreach (Transform tr in tgo)
                {
                    if (tr.GetComponent<T>())
                        meshfilters.Add(tr.GetComponent<T>());
                }
               // Debug.Log("meshfilters count : " + meshfilters.Count());
            }
            return (T[])meshfilters.ToArray();
        }
        else
            return (T[])FindObjectsOfType(typeof(T));
    }

    private TextureDetails GetTextureDetail(Texture tTexture, Material tMaterial, MaterialDetails tMaterialDetails)
    {
        TextureDetails tTextureDetails = GetTextureDetail(tTexture);

        tTextureDetails.FoundInMaterials.Add(tMaterial);
        foreach (Renderer renderer in tMaterialDetails.FoundInRenderers)
        {
            if (!tTextureDetails.FoundInRenderers.Contains(renderer)) tTextureDetails.FoundInRenderers.Add(renderer);
        }
        return tTextureDetails;
    }

    private TextureDetails GetTextureDetail(Texture tTexture, Renderer renderer)
    {
        TextureDetails tTextureDetails = GetTextureDetail(tTexture);

        tTextureDetails.FoundInRenderers.Add(renderer);
        return tTextureDetails;
    }

    private TextureDetails GetTextureDetail(Texture tTexture, Animator animator)
    {
        TextureDetails tTextureDetails = GetTextureDetail(tTexture);

        tTextureDetails.FoundInAnimators.Add(animator);
        return tTextureDetails;
    }

    private TextureDetails GetTextureDetail(Texture tTexture, Graphic graphic)
    {
        TextureDetails tTextureDetails = GetTextureDetail(tTexture);

        tTextureDetails.FoundInGraphics.Add(graphic);
        return tTextureDetails;
    }

    private TextureDetails GetTextureDetail(Texture tTexture, MonoBehaviour script)
    {
        TextureDetails tTextureDetails = GetTextureDetail(tTexture);

        tTextureDetails.FoundInScripts.Add(script);
        return tTextureDetails;
    }
    private bool GetFlag(int num)
    {
        if (num < 1) return false;
        return (num & num - 1) == 0;
    }
    private TextureDetails GetTextureDetail(Texture tTexture)
    {
        TextureDetails tTextureDetails = FindTextureDetails(tTexture);
        if (tTextureDetails == null)
        {
            tTextureDetails = new TextureDetails();
            tTextureDetails.texture = tTexture;
            tTextureDetails.isCubeMap = tTexture is Cubemap;
            if (GetFlag(tTexture.width) && GetFlag(tTexture.height))
                tTextureDetails.npot = true;
            else
                tTextureDetails.npot = false;
            tTextureDetails.wrapmode = tTexture.wrapMode;
            int memSize = CalculateTextureSizeBytes(tTexture);
            TextureFormat tFormat = TextureFormat.RGBA32;
            int tMipMapCount = 1;
            if (tTexture is Texture2D)
            {
                tFormat = (tTexture as Texture2D).format;
                tMipMapCount = (tTexture as Texture2D).mipmapCount;
            }
            if (tTexture is Cubemap)
            {
                tFormat = (tTexture as Cubemap).format;
                memSize = 8 * tTexture.height * tTexture.width;
            }
            if (tTexture is Texture2DArray)
            {
                tFormat = (tTexture as Texture2DArray).format;
                tMipMapCount = 10;
            }

            tTextureDetails.memSizeKB = memSize / 1024;
            tTextureDetails.format = tFormat;
            tTextureDetails.mipMapCount = tMipMapCount;

            string path = AssetDatabase.GetAssetPath(tTexture);
            TextureImporter A = (TextureImporter)AssetImporter.GetAtPath(path);
            if (A)
            {
                tTextureDetails.issRGB = A.sRGBTexture;
                tTextureDetails.isReadWriteEnable = A.isReadable;
                tTextureDetails.compress = A.textureCompression;
            }

        }
        return tTextureDetails;
    }

}
