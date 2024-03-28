using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.U2D;
using System.Linq;
using UnityEditor.U2D;
using System.IO;
using System.Reflection;

namespace AtlasEditor
{
    public class SpriteComparer : Comparer<Sprite>
    {
        public override int Compare(Sprite a, Sprite b)
        {
            int weight_a = a.texture.width + a.texture.height;
            int weight_b = b.texture.width + b.texture.height;

            if (weight_a < weight_b)
                return -1;
            else
                return 1;
        }
    }

    public class Row
    {
        public int width;
        public int height;
        public List<Sprite> sprites;
        public List<Vector2> poss;
        public Vector2 pos;

        public Row(Vector2 startPos)
        {
            this.pos = startPos;
            sprites = new List<Sprite>();
            poss = new List<Vector2>();
        }

        public void Add(Sprite sprite)
        {
            int w = sprite.texture.width;
            int h = sprite.texture.height;

            poss.Add(pos);
            pos += new Vector2(w, 0);

            sprites.Add(sprite);
            width += w + SpriteAtlasEditor.defaultSpacing.x;
            height = height > h ? height : h;
        }

        public Vector2 GetPos(int index)
        {
            if (index > poss.Count)
                return Vector2.zero;

            return poss[index];
        }

        public Vector2 GetCenter(int index)
        {
            Vector2 pos = GetPos(index);
            Sprite sprite = sprites[index];
            return pos + new Vector2(sprite.texture.width / 2, sprite.texture.height / 2);
        }

        public void Dispose()
        {
            sprites.Clear();
        }
    }

    public class SpriteAtlasEditor : EditorWindow
    {
        private static int WIDTH = 1920;
        private static int HEIGHT = 1080;
        private string _atlasName = "";
        private string _searchSprite = "";
        private string _searchAtlas = "";
        private SpriteAtlas _spriteAtlas;

        private Vector2 _spriteScrollPos;
        private Vector2 _atlasScrollPos;
        private List<Sprite> _sprites;
        private List<Row> _rows;
        private List<Sprite> _deletes;
        private List<string> _atlasPaths;
        private Dictionary<Sprite, Color> _colors;

        //Editor的排布组件 默认间距
        private static Vector2Int _defaultSpacing = new Vector2Int(3, 2);
        private Vector2Int _spriteStartPos = new Vector2Int(240, 30);
        private Vector2Int _atlasStartPos = new Vector2Int(0, 200);
        private Vector2Int _atlasItemSize = new Vector2Int(220, 25);
        private Sprite _mouseSprite;

        public static Vector2Int defaultSpacing
        {
            get { return _defaultSpacing; }
        }

        SpriteAtlasEditor()
        {
            this.titleContent = new GUIContent("图集编辑器");
        }

        [MenuItem("Assets/SpriteAtlasTools")]
        static void ShowWindows()
        {
            EditorWindow.GetWindowWithRect(typeof(SpriteAtlasEditor), new Rect(0, 0, WIDTH, HEIGHT));
        }

        private void Awake()
        {
            Init();
            _atlasPaths = Utils.GetAllFileList(Application.dataPath, ".spriteatlas");
            for (int i = 0; i < _atlasPaths.Count; i++)
                _atlasPaths[i] = Utils.SystemPath2ArtworkPath(_atlasPaths[i]);

            FindSprites();
            UpdateDraw();
        }

        private void Init()
        {
            _rows = new List<Row>();
            _deletes = new List<Sprite>();
            _sprites = new List<Sprite>();
            _atlasPaths = new List<string>();
            _colors = new Dictionary<Sprite, Color>();
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical();
            #region 图集信息
            GUIStyle style = new GUIStyle();
            style.fontSize = 18;
            style.normal.textColor = Color.white;
            int y = 0;
            GUI.Label(new Rect(0, y, WIDTH, 20), $"当前图集信息: {_atlasName}.spriteatlas、 图片数量: {_sprites.Count}、图集尺寸: {TextureDebugInfo()}", style);
            #endregion

            #region 图集内的图片搜索
            GUI.Label(new Rect(0, y += 25, WIDTH, 20), $"图集内的图片搜索: {GetDrawedSpriteCount()}", style);
            _searchSprite = GUI.TextField(new Rect(0, y += 25, 240, 20), _searchSprite);
            Search();
            #endregion

            #region 项目内的图集搜索
            GUI.Label(new Rect(0, y += 25, WIDTH, 20), "图集搜索", style);
            _searchAtlas = GUI.TextField(new Rect(0, y += 25, 240, 20), _searchAtlas);
            #endregion

            #region 操作按钮
            style = new GUIStyle(GUI.skin.button);
            style.fontSize = 18;
            if (GUI.Button(new Rect(0, y += 25, 120, 30), "删除", style))
            {
                Delete();
            }

            if (GUI.Button(new Rect(120, y, 120, 30), "保存", style))
            {
                Save();
            }

            #endregion

            GUILayout.BeginArea(new Rect(_spriteStartPos.x, _spriteStartPos.y, position.width - _spriteStartPos.x, position.height - _spriteStartPos.y));
            _spriteScrollPos = GUILayout.BeginScrollView(_spriteScrollPos);
            DrawRow();
            GUILayout.EndScrollView();
            GUILayout.EndArea();


            GUILayout.BeginArea(new Rect(_atlasStartPos.x, _atlasStartPos.y, 240, position.height - _atlasStartPos.y));
            _atlasScrollPos = GUILayout.BeginScrollView(_atlasScrollPos);
            DrawAtlas();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUILayout.EndVertical();
            Drag();
        }

        private void Delete()
        {
            if (_spriteAtlas == null || _deletes.Count == 0)
                return;

            foreach(Sprite sprite in _deletes)
            {
                _spriteAtlas.Remove(new UnityEngine.Object[] { sprite.texture });
                _sprites.Remove(sprite);
            }

            _deletes.Clear();
            UpdateDraw();
        }

        private void Save()
        {
            if (_spriteAtlas == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(_spriteAtlas);
            _spriteAtlas = Instantiate(_spriteAtlas);
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(_spriteAtlas, assetPath);
            AssetDatabase.SaveAssets();
            SpriteAtlasUtility.PackAtlases(new SpriteAtlas[] { _spriteAtlas }, BuildTarget.StandaloneWindows64);
        }

        private void Search()
        {
            if (string.IsNullOrEmpty(_searchSprite))
            {
                int count = GetDrawedSpriteCount();
                if (count != _sprites.Count)
                    UpdateDraw();

                return;
            }

            if (_sprites == null)
                return;

            List<Sprite> sprites = new List<Sprite>();
            foreach (Sprite sprite in _sprites)
            {
                if (sprite.texture.name.Contains(_searchSprite))
                    sprites.Add(sprite);
            }

            UpdateDraw(sprites);
        }

        private void DrawAtlas()
        {
            if (string.IsNullOrEmpty(_searchAtlas))
            {
                DrawAtlas(_atlasPaths);
                return;
            }

            List<string> paths = new List<string>();
            foreach (string path in _atlasPaths)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName.Contains(_searchAtlas))
                    paths.Add(path);
            }

            DrawAtlas(paths);
        }

        private void DrawAtlas(List<string> atlasPath)
        {
            atlasPath = atlasPath.OrderBy(x => Path.GetFileNameWithoutExtension(x)[0]).ToList();
            foreach (string path in atlasPath)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                GUIStyle style = new GUIStyle(GUI.skin.button);
                style.fontSize = 14;
                bool click = GUILayout.Button(fileName, style, GUILayout.Width(_atlasItemSize.x), GUILayout.Height(_atlasItemSize.y));
                if (click) OnAtlasClick(path);
            }
        }

        private void DrawRow()
        {
            GUILayout.BeginVertical();
            for (int i = 0; i < _rows.Count; i++)
            {
                DrawSprites(_rows[i], i);
            }
            GUILayout.EndVertical();
        }

        private void DrawSprites(Row row, int rowIndex = 0)
        {
            _mouseSprite = null;
            GUILayout.BeginHorizontal();
            int offset = 0;
            for (int i = 0; i < row.sprites.Count; i++)
            {
                Sprite sprite = row.sprites[i];
                bool click = GUILayout.Button(sprite.texture, GUILayout.Width(sprite.texture.width), GUILayout.Height(sprite.texture.height));
                Vector2 screenPos = row.GetCenter(i);
                Handles.BeginGUI();
                Handles.color = GetColor(sprite);
                Handles.DrawWireCube(new Vector3(screenPos.x + offset, screenPos.y + rowIndex * _defaultSpacing.y, 0), new Vector3(sprite.texture.width, sprite.texture.height, 0));
                Handles.EndGUI();
                offset += _defaultSpacing.x;

                if (click) OnSpriteClick(sprite);
                if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)) OnSpriteEnter(sprite);
            }
            GUILayout.EndHorizontal();
        }

        private void Drag()
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.DragUpdated)
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (Object draggedObject in DragAndDrop.objectReferences)
                {
                    if (draggedObject is Texture2D)
                    {
                        Texture2D tex = draggedObject as Texture2D;
                        if (!_sprites.FirstOrDefault(sprite => sprite.texture == tex))
                        {
                            _spriteAtlas.Add(new UnityEngine.Object[] { tex });
                            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
                            _sprites.Add(sprite);
                        }
                    }
                }

                UpdateDraw();
            }
        }

        private void Follow()
        {
            if (_mouseSprite == null)
                return;

            int width = 100;
            int height = 50;
            Vector2 mousePosition = Event.current.mousePosition;
            Rect rect = new Rect(Mathf.Min(mousePosition.x + 20, WIDTH - 400), mousePosition.y, width, height);
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.red;
            style.fontStyle = FontStyle.Bold;
            style.fontSize = 18;
            Color outlineColor = Color.white;
            Handles.color = outlineColor;
            GUI.Label(rect, $"{_mouseSprite.texture.name}", style);
        }
        
        private void OnAtlasClick(string assetPath)
        {
            SpriteAtlas spriteAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(assetPath);
            if(spriteAtlas== null) return;

            if (Event.current.button == 0)
            {
                Reset();
                UpdateSpriteAtlas(spriteAtlas);
                UpdateDraw(_sprites);
            }
            else if (Event.current.button == 1)
            {
                EditorGUIUtility.PingObject(spriteAtlas);
                Selection.activeObject = spriteAtlas;
            }
        }

        private void OnSpriteEnter(Sprite sprite)
        {
            _mouseSprite = sprite;
        }

        private void OnSpriteClick(Sprite sprite)
        {
            if(Event.current.button == 0)
            {
                if (_colors.ContainsKey(sprite))
                    _colors.Remove(sprite);
                else
                    _colors[sprite] = Color.red;

                if (_deletes.Contains(sprite))
                    _deletes.Remove(sprite);
                else
                    _deletes.Add(sprite);
            }
            else if(Event.current.button == 1)
            {
                EditorGUIUtility.PingObject(sprite.texture);
                Selection.activeObject = sprite.texture;
            }
        }

        private int GetDrawedSpriteCount()
        {
            int length = _rows.Sum(row => row.sprites.Count);
            return length;
        }

        private int GetAtlasDrawHeight()
        {
            return _atlasPaths.Count * _atlasItemSize.y;
        }

        private int GetSpriteDrawHeight()
        {
            int height = _rows.Sum(row => row.height + _defaultSpacing.y);
            return height;
        }

        private Color GetColor(Sprite sprite)
        {
            if (_colors.ContainsKey(sprite))
                return _colors[sprite];

            return Color.green;
        }

        private void UpdateDraw(List<Sprite> sprites = null)
        {
            if (_sprites == null)
                return;

            if (sprites == null) sprites = _sprites;
            sprites.Sort(new SpriteComparer());
            _rows.Clear();

            Row row = new Row(_defaultSpacing);
            _rows.Add(row);

            float height = 0;
            foreach (Sprite sprite in sprites)
            {
                if(row.width + sprite.texture.width >= WIDTH - _spriteStartPos.x)
                {
                    height += row.height;
                    row = new Row(new Vector2(_defaultSpacing.x, height + _defaultSpacing.y));
                    _rows.Add(row);
                }

                row.Add(sprite);
            }
        }

        private void FindSprites()
        {
            if (Selection.activeObject == null)
                return;

            SpriteAtlas spriteAtlas = Selection.activeObject as SpriteAtlas;
            UpdateSpriteAtlas(spriteAtlas);
        }

        private void UpdateSpriteAtlas(SpriteAtlas spriteAtlas)
        {
            _spriteAtlas = spriteAtlas;
            FindSprites(_spriteAtlas);
        }

        private void FindSprites(SpriteAtlas spriteAtlas)
        {
            if (spriteAtlas == null)
                return;

            Sprite[] sprites = new Sprite[spriteAtlas.spriteCount];
            spriteAtlas.GetSprites(sprites);
            _sprites.AddRange(sprites);
            _sprites.RemoveAll(sprite => sprite == null);
            _atlasName = spriteAtlas.name;
        }

        public string TextureDebugInfo()
        {
            Texture2D[] texture2Ds = GetPreviewTextures(_spriteAtlas);
            if (texture2Ds == null || texture2Ds.Length == 0)
                return "";

            return $"{texture2Ds[0].width}*{texture2Ds[0].height}*{texture2Ds.Length}";
        }

        private Texture2D[] GetPreviewTextures(SpriteAtlas spriteAtlas)
        {
            if (spriteAtlas == null)
                return null;

            MethodInfo methodInfo = typeof(SpriteAtlasExtensions).GetMethod("GetPreviewTextures", BindingFlags.NonPublic | BindingFlags.Static);
            if (methodInfo != null)
                return methodInfo.Invoke(null, new SpriteAtlas[] { spriteAtlas }) as Texture2D[];

            return null;
        }

        private void OnDestroy()
        {
            Reset();
        }

        private void Reset()
        {
            _spriteAtlas = null;
            _sprites?.Clear();
            _rows?.Clear();
            _deletes?.Clear();
            _colors?.Clear();
        }
    }
}