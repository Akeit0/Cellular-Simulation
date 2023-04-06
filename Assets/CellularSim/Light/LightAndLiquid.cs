using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UI;
using CellularSim.Liquid;
using Random = UnityEngine.Random;
using ZimGui;
namespace CellularSim.Light {
    public class LightAndLiquid : MonoBehaviour {
        public enum BrushMode {
            Block,
            Water,
            Eraser,
            BlackHole,
            WaterSource
        }

        string[] _brushModes={ "Block : B ",
            "Water:W",
            "Eraser : E",
            "BlackHole : D",
            "WaterSource: S"};
        
        public Image Image;
        Texture2D _lightTexture;
        Texture2D _noiseTexture;
        LightMap _lightMap ;
        [SerializeField] BrushMode _brushMode=BrushMode.Block;
        public int Width=200;
        public int Height=200;
        int _width=200;
        int _height=200;
        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        static readonly int Map = Shader.PropertyToID("_LightMap");        
         NativeArray<Cell> _cellArray;
         NativeArray<DiffsUD> _diffsArray;
         NativeArray<Color24> _lightSourceArray;
        public void Start() {
            Application.targetFrameRate = 60;
            _liquidTime = 0;
            Random.InitState(Time.frameCount);
            _width = Width;
            _height = Height;
            _lightTexture = new Texture2D(_width, _height, TextureFormat.RGB24, false) {
                filterMode = FilterMode.Point
            };
            var material = Image.material;
            material.SetTexture(Map,_lightTexture); 
            
            _noiseTexture=  new Texture2D(_width, _height, TextureFormat.RGB24, false) {
                filterMode = FilterMode.Point
            };
            material.SetTexture(MainTex,_noiseTexture);
            _lightMap= new LightMap(_width, _height);
            CreateGrid();
            _wObject=IM.Add("Setting : Toggle with Esc",DrawSettings);
            _wObject.Opened = true;
            _wObject.Rect = new Rect(Screen.width - 400, 0 , 400,  Screen.height);
            IMStyle.FloatFormat = null;
            IMStyle.FontSize = 15;
        }


        WObject _wObject;
        void CreateGrid() {
            _cellArray = new NativeArray<Cell>(_width * _height,Allocator.Persistent);
            _diffsArray = new NativeArray<DiffsUD>(_width * _height,Allocator.Persistent);
            _lightSourceArray = new NativeArray<Color24>(_width * _height, Allocator.Persistent);

        }
        
        public FilterMode LightFilterMode = FilterMode.Point;
       
          void Update() {
              if (Input.GetKeyDown(KeyCode.Space)) {
                  _useBlockLight ^= true;
                  radiusTime = 0.3f;
              }
              if (Input.GetKeyDown(KeyCode.B)) {
                  _brushMode = BrushMode.Block;
                  radiusTime = 0.3f;
              }else if (Input.GetKeyDown(KeyCode.W)) {
                  _brushMode = BrushMode.Water;
                  radiusTime = 0.3f;
              }else if (Input.GetKeyDown(KeyCode.E)) {
                  _brushMode = BrushMode.Eraser;
                  radiusTime = 0.3f;
              }else if (Input.GetKeyDown(KeyCode.D)) {
                  _brushMode = BrushMode.BlackHole;
                  radiusTime = 0.3f;
              }else if (Input.GetKeyDown(KeyCode.S)) {
                  _brushMode = BrushMode.WaterSource;
                  radiusTime = 0.3f;
              }
              if (Input.GetKeyDown(KeyCode.Escape)) {
                  _wObject.Opened =! _wObject.Opened;
              }
            _lightTexture.filterMode = LightFilterMode;
             _lightMap.Clear();
             if (IMInput.TargetID!=_wObject.WindowID&&Image.rectTransform.TryGetMousePosition(Camera.main,out var mousePosInRect,out var rect)) {
               
                 Draw(rect,mousePosInRect);
                 _lightSourceArray.CopyTo(_lightMap.Colors);
                 if (_useMouseLight) {
                     var x = (int) (_width * mousePosInRect.x);
                     var y =(int) (_height*mousePosInRect.y);
                     _lightMap[ x,y] = new Color24(BlockLightColor);
                 }
               
             }
             else {
                 _lightSourceArray.CopyTo(_lightMap.Colors);
             }
             _lastMousePos = Input.mousePosition;
             WriteMap();
             if(DoLight) {
                 if(!_doLightLast) {
                     var material = Image.material;
                     material.SetTexture(Map, _lightTexture);
                     _doLightLast = true;
                 }
                
                 _lightMap.Blur();
                 _lightMap.CopyTo(_lightTexture.GetRawTextureData<Color24>());
                 _lightTexture.Apply();
             }
             else {
                 if(_doLightLast) {
                     var material = Image.material;
                     material.SetTexture(Map, null);
                     _doLightLast = false;
                 }
             }
            
        }
        [Range(1,255)]
        public int AirAttenuation  = 12;
        [Range(1,255)]
        public int BlockAttenuation = 75;
        [Range(1,200)]
        public int BrushSize = 1;

        public Color24 BlockLightColor24 => _useBlockLight ? new Color24(BlockLightColor):default;
        public Color BlockLightColor;
        public bool DoPhys;
        public bool DoLight;
        public bool DoReplace;
        bool _doLightLast;
      
        Vector2 _lastMousePos;
        bool _colorFO;
        bool _useHSV;
        bool _useBlockLight;
        bool _useMouseLight;

        bool _showInstructionsFO=true;
        bool _baseSettingsFO=true;
        bool _waterSettingsFO;
        bool _lightSettingsFO;
        int _liquidFrameRate = 60;
        float _liquidTime;
        int _currentFPS;
        float _currentFPSTime;
        string[] _filerModes = new string[] {"Point", "Bilinear", "Trilinear"};
        bool DrawSettings() {
            if (IM.Foldout("Instructions", ref _showInstructionsFO)) {
                IM.Label("This is a demo for cellular simulation.");
                IM.Label("Left Mouse is used to use brush.");
                IM.Label("Right Mouse is used to move the image.");
                IM.Label("Mouse Wheel is used to zoom in/out on the image.");
                IM.Label("KeyBoards is used for shortcuts.");
                IM.Label("e.g. B : Block, W : Water.");
            }

            if(IM.Foldout("Game",ref _baseSettingsFO)) {
                if ((_currentFPSTime -= Time.unscaledDeltaTime) < 0) {
                    _currentFPSTime = 0.1f;
                    _currentFPS = (int) (1f / Time.unscaledDeltaTime);
                }
                IM.Label<Int32ParserFormatter,int>("FPS  :  ",_currentFPS);
                var frameRate = Application.targetFrameRate;
                if (IM.IntField("TargetFPS", ref frameRate, 15, 300)) {
                    Application.targetFrameRate = frameRate;
                }
                IM.IntField("LiquidTargetFPS", ref _liquidFrameRate, 1, 300);
                IM.FloatField("FontSize", ref IMStyle.FontSize, 10, 30);
                IM.BoolField("WaterSim", ref DoPhys);
                IM.BoolField("LightingSim", ref DoLight);
                IM.Slider("BrushSize : Shift+Scroll", ref BrushSize,1,200);
                var mode = (int) _brushMode;
                IM.DropDownField("BrushMode", _brushModes, ref mode);
                _brushMode =(BrushMode) mode;
            }
            if(IM.Foldout("Water",ref _waterSettingsFO)) {
                using (IM.Indent()) {
                    IM.FloatField("LiquidPerFrame", ref liquidPerClick, 0, 10000);
                    IMStyle.DragNumberScale = 0.1f;
                    IM.Slider("FlowSpeed", ref FlowSpeed, 0, 1);
                    IMStyle.DragNumberScale = 1f;
                    IM.Slider("FlowSpeed2", ref HorizontalFlowFactor, 1, 10);
                    IMStyle.DragNumberScale = 0.1f;
                    IM.Slider("Compression", ref Compression, 0, 1);
                    IMStyle.DragNumberScale = 1f;
                }
               
            }

            if (IM.Foldout("Light",ref _lightSettingsFO)) {
                using (IM.Indent()) {
                    IMStyle.DragNumberScale = 0.1f;
                    var filer = (int) LightFilterMode;
                    IM.DropDownField("FilterMode", _filerModes, ref filer);
                    LightFilterMode =(FilterMode) filer;
                    IM.BoolField("BlockLightColor24 : Space", ref _useBlockLight);
                    IM.BoolField("MouseLight", ref _useMouseLight);
                    if (IM.Foldout("BlockLightColor", ref _colorFO)) {
                        using (IM.Indent()) {
                            IM.Quad(BlockLightColor);
                            IM.BoolField("HSV", ref _useHSV);
                            if (_useHSV) {
                                Color.RGBToHSV(BlockLightColor, out var h, out var s, out var v);
                                IM.Slider("H", ref h, 0, 1);
                                IM.Slider("S", ref s, 0, 1);
                                IM.Slider("V", ref v, 0, 1);
                                BlockLightColor = Color.HSVToRGB(h, s, v);
                            }
                            else {
                                IM.Slider("R", ref BlockLightColor.r, 0, 1);
                                IM.Slider("G", ref BlockLightColor.g, 0, 1);
                                IM.Slider("B", ref BlockLightColor.b, 0, 1);
                            }
                        }
                    }
                    IMStyle.DragNumberScale = 1f;
                    IM.Slider("AirAttenuation", ref AirAttenuation, 1, 255);
                    IM.Slider("BlockAttenuation", ref BlockAttenuation, 1, 255);
                   
                }
            }
            return true;
        }

        float radiusTime;
        void Draw(Rect rect,Vector2 mousePosInRect) {
            var scroll = Input.mouseScrollDelta.y;
            Vector2 mousePos = Input.mousePosition;
           
            if (scroll!=0) {
                if (Input.GetKey(KeyCode.LeftShift)) {
                    BrushSize += (int) (Mathf.Sign(scroll) * (10+BrushSize)/10f);
                    BrushSize = Math.Clamp(BrushSize, 1, 200);
                    radiusTime = 0.3f;
                }else {
                    var rectTransform = Image.rectTransform;
                    var pastScale = rectTransform.localScale;
                    var newScale = pastScale * (1f + scroll / 10f);
                    if (newScale.x is > 0.2f and < 20) {
                        rectTransform.localScale = newScale;
                        Vector3 offsetPos =
                            new Vector3(
                                (mousePosInRect.x - 0.5f) * rect.width * (newScale.x - pastScale.x) / pastScale.x,
                                (mousePosInRect.y - 0.5f) * rect.height * (newScale.y - pastScale.y) / pastScale.y);
                        rectTransform.localPosition -= offsetPos;
                    }
                }
            }
            if (0 < radiusTime) {
                radiusTime -= Time.unscaledDeltaTime;
                var radius = BrushSize * rect.width / _width / 2;
                IM.Circle(mousePos, radius, new UiColor(0x00FF00FF));
                UiColor brushColor;
                var lightFlag = false;
                switch (_brushMode) {
                    case BrushMode.Block: brushColor = new UiColor(116, 97, 82);
                        lightFlag = true;
                        break;
                    case BrushMode.Eraser:brushColor = new UiColor(255, 255, 255);
                        break;
                    case BrushMode.BlackHole:brushColor = new UiColor(0, 0, 0);
                        lightFlag = true;
                        break;
                    case BrushMode.Water:brushColor = new UiColor(0, 0, 255);
                        break;
                    case BrushMode.WaterSource:brushColor = new UiColor(0, 255, 255);
                        lightFlag = true;
                        break;
                    default:brushColor = default;
                        break;
                }
                IM.Circle(mousePos, radius*0.8f, brushColor);
                if (lightFlag&&_useBlockLight) {
                    IM.Mesh.AddQuad(mousePos+new Vector2(radius/2,0),
                        mousePos+new Vector2(radius/8,radius/2),
                        mousePos+new Vector2(radius/2,radius),
                        mousePos+new Vector2(radius*0.875f,radius/2),
                        BlockLightColor);
                }
            }
            if ((Input.GetMouseButton(1))&&_lastMousePos != mousePos) {
                var pos = Image.rectTransform.localPosition;
                Image.rectTransform.localPosition = pos + (Vector3)(mousePos - _lastMousePos);
               return;
            } 
        
           

          
            var x =  (_width * mousePosInRect.x);
            var y =(_height*mousePosInRect.y);
            // Check if we are filling or erasing walls
            if (Input.GetMouseButton(0))
            {
                if ((x >= 0 && x < _width) && (y >= 0 && y < _height)) {
                    var gridIndex = CalculateCellIndex((int)x, (int)y, _width);
                    switch (_brushMode) {
                        case BrushMode.Block: {
                            if(BrushSize==1) {
                                _cellArray[gridIndex] = Cell.Solid();
                                _lightSourceArray[gridIndex] = BlockLightColor24;
                            }
                            else 
                                FillCircleWithCell(x,y,BrushSize,Cell.Solid(),BlockLightColor24);
                            break;
                        }
                        case BrushMode.Water: {
                            if(BrushSize==1) {
                                ref var clickedCell = ref (_cellArray.ElementAt(gridIndex));
                                if (!DoReplace && clickedCell.CellType != 0) return;
                                clickedCell.CellType = 0;
                                clickedCell.Liquid += (short) (MaxLiquid * liquidPerClick);
                            }
                            else {
                                ForCircleAddLiquid(x,y,BrushSize);
                            }
                            break;
                        }
                        case BrushMode.Eraser: {
                            if(BrushSize==1) {
                                _cellArray[gridIndex] = new Cell();
                                _lightSourceArray[gridIndex] = default;
                            }
                            else {
                                FillCircleWithCell(x,y,BrushSize,default,default);
                            }
                            break;
                        }
                        case BrushMode.BlackHole: {
                            if(BrushSize==1) {
                                _cellArray[gridIndex] = Cell.Hole();
                                _lightSourceArray[gridIndex] =  BlockLightColor24;
                            }
                            else 
                                FillCircleWithCell(x,y,BrushSize,Cell.Hole(),BlockLightColor24);
                            break;
                        }
                        case BrushMode.WaterSource: {
                            if(BrushSize==1) {
                                _cellArray[gridIndex] = Cell.Source((short) (MaxLiquid * liquidPerClick));
                                _lightSourceArray[gridIndex] = BlockLightColor24;
                            }
                            else {
                                FillCircleWithCell(x, y, BrushSize, Cell.Source((short) (MaxLiquid * liquidPerClick)),BlockLightColor24);
                            }
                            break;
                        }
                    }
                    
                }
                
            }
        }
        public float liquidPerClick = 2;
        [Range(0,1)]
        public float FlowSpeed=0.8f;
        [Range(0,1)]
        public float Compression=0.03f;
         [Range(1,10)]
        public int HorizontalFlowFactor=3;
        
        
        
        public const int MaxLiquid=512;
        void WriteMap() {
            var inputDeps = default(JobHandle);
            if (DoPhys) {
                _liquidTime+= Time.unscaledDeltaTime;
                _liquidTime = Mathf.Clamp(_liquidTime, 0, 2f);
                var count = (int)(_liquidTime * _liquidFrameRate);
                if (10 < count) {
                    count = 10;
                    _liquidTime =0;
                }else{
                    _liquidTime -=  (float)count/_liquidFrameRate;
                }
                
                for (var i=0;i<count;i++) {
                    inputDeps = new CalculateWaterPhysics() {
                        Cells = _cellArray,
                        Diffs = _diffsArray,
                        MaxLiquid = MaxLiquid,
                        MinLiquid = 4,
                        Compression = (int) (MaxLiquid * Compression),
                        // MinFlow = MinFlow,
                        FlowSpeed = FlowSpeed,
                        HorizontalFlowFactor = HorizontalFlowFactor,
                        GridWidth = _width,
                        GridHeight = _height,
                    }.Schedule(_height, 1,inputDeps);
                    //Apply Water Physics
                    inputDeps = new ApplyWaterPhysics() {
                        Diffs = _diffsArray,
                        Cells = _cellArray,
                        MinLiquid = 4,
                        GridWidth = _width,
                        GridHeight = _height,
                    }.Schedule(_height, 1, inputDeps);
                }
                
            }
            inputDeps = new CellToDataJob {
                MapArray = _noiseTexture.GetRawTextureData<Color24>(),
                MediumArray = _lightMap.Medium,
                Cells = _cellArray,
                AirTransmittance= (byte)(256 - AirAttenuation),
                BlockTransmittance =(byte)(256 - BlockAttenuation),
            }.Schedule(_cellArray.Length, 32, inputDeps);
            inputDeps.Complete();
            _noiseTexture.Apply();
        }
        [BurstCompile]
        struct CellToDataJob:IJobParallelFor {
            [WriteOnly]
            public NativeArray<Color24> MapArray;
            [WriteOnly]
            public NativeArray<Color24> MediumArray;
            [ReadOnly]
            public NativeArray<Cell> Cells;
            public byte AirTransmittance;
            public byte BlockTransmittance;
            public void Execute(int index) {
                Color24 mapColor; 
                Color24 mediumColor;
                if (Cells[index].CellType == 0) {
                    var liquid = Cells[index].Liquid;
                    if (liquid == 0) {
                        mapColor = new Color24(255, 255, 255);
                        mediumColor = new Color24(AirTransmittance, AirTransmittance, AirTransmittance);
                    }
                    else {
                        if (liquid < MaxLiquid) {
                            var w = (byte) Math.Min(128 - liquid * 128 / MaxLiquid, 128);
                            mapColor = new Color24(w, w, 255);
                        }
                        else {
                            var b = (byte) Math.Max(383 - liquid * 128 / MaxLiquid, 128);
                            mapColor = new Color24(0, 0, b);
                        }

                        var waterRG = (byte) (AirTransmittance - 10);
                        mediumColor = new Color24(waterRG, waterRG, AirTransmittance);
                    }
                }
                else if (Cells[index].CellType == CellType.Solid) {
                    mapColor = Cells[index].Liquid == 0
                        ? new Color24(116, 97, 82)
                        : new Color24(0, 255, 255);
                    mediumColor = new Color24(BlockTransmittance, BlockTransmittance, BlockTransmittance);
                }
                else {
                    mapColor = default;
                    mediumColor = new Color24(BlockTransmittance, BlockTransmittance, BlockTransmittance);
                }
                MapArray[index] = mapColor;
                MediumArray[index] = mediumColor;
            }
        }
        void ForCircleAddLiquid(float centerX ,float centerY,int brushSize) {
            var radius = brushSize / 2;
            var perPixel = MaxLiquid*liquidPerClick;
            if (brushSize%2==1) {
                var centerXInt = (int) centerX;
                var centerYInt = (int) centerY;
                var minX = Mathf.Max(centerXInt - radius, 0);
                var  maxX = Mathf.Min(minX+brushSize+1, _width);
                var  minY = Mathf.Max(centerYInt - radius, 0);
                var  maxY = Mathf.Min(minY+brushSize+1, _height);
                var sqrRadius = (radius * radius + radius);
                for (int x =minX; x < maxX; x++) {
                    for (int y = minY; y < maxY; y++) {
                        if((x-centerXInt)*(x-centerXInt)+(y-centerYInt)*(y-centerYInt)<sqrRadius) {
                            var gridIndex = CalculateCellIndex(x, y, _width);
                            ref var  clickedCell =ref  (_cellArray.ElementAt(gridIndex));
                            if (!DoReplace && clickedCell.CellType != 0) continue;
                            clickedCell.CellType = 0;
                            clickedCell.Liquid = (short) Math.Min(perPixel + clickedCell.Liquid, short.MaxValue);
                        }
                    }
                }
            }
            else {
                centerX =  Mathf.Round(centerX);
                centerY =Mathf.Round(centerY);
                var minX = Mathf.Max( (int)centerX - radius, 0);
                var  maxX = Mathf.Min(minX+brushSize, _width);
                var  minY = Mathf.Max( (int)centerY - radius, 0);
                var  maxY = Mathf.Min(minY+brushSize, _height);
                var sqrRadius =(float)  (radius * radius + radius);
                for (int x =minX; x < maxX; x++) {
                    for (int y = minY; y < maxY; y++) {
                        if((x-centerX+0.5f)*(x-centerX+0.5f)+(y-centerY+0.5f)*(y-centerY+0.5f)<sqrRadius) {
                            var gridIndex = CalculateCellIndex(x, y, _width);
                            ref var  clickedCell =ref  (_cellArray.ElementAt(gridIndex));
                            if (!DoReplace && clickedCell.CellType != 0) continue;
                            clickedCell.CellType = 0;
                            clickedCell.Liquid = (short) Math.Min(perPixel + clickedCell.Liquid, short.MaxValue);
                        }
                    }
                }
            }
            
        } 
        void FillCircleWithCell(float centerX ,float centerY,int brushSize,Cell cell,Color24 color24) {
            var radius = brushSize / 2;
            if (brushSize%2==1) {
                var centerXInt = (int) centerX;
                var centerYInt = (int) centerY;
               var minX = Mathf.Max(centerXInt - radius, 0);
               var  maxX = Mathf.Min(minX+brushSize+1, _width);
               var  minY = Mathf.Max(centerYInt - radius, 0);
               var  maxY = Mathf.Min(minY+brushSize+1, _height);
                var sqrRadius = (radius * radius + radius);
                for (int x =minX; x < maxX; x++) {
                    for (int y = minY; y < maxY; y++) {
                        if((x-centerXInt)*(x-centerXInt)+(y-centerYInt)*(y-centerYInt)<sqrRadius) {
                            var gridIndex = CalculateCellIndex(x, y, _width);
                            _cellArray[gridIndex] = cell;
                            _lightSourceArray[gridIndex] = color24;
                        }
                    }
                }
            }
            else {
                centerX =  Mathf.Round(centerX);
                centerY =Mathf.Round(centerY);
                var minX = Mathf.Max( (int)centerX - radius, 0);
                var  maxX = Mathf.Min(minX+brushSize, _width);
                var  minY = Mathf.Max( (int)centerY - radius, 0);
                var  maxY = Mathf.Min(minY+brushSize, _height);
                var sqrRadius =(float)  (radius * radius + radius);
                for (int x =minX; x < maxX; x++) {
                    for (int y = minY; y < maxY; y++) {
                        if((x-centerX+0.5f)*(x-centerX+0.5f)+(y-centerY+0.5f)*(y-centerY+0.5f)<sqrRadius) {
                            var gridIndex = CalculateCellIndex(x, y, _width);
                            _cellArray[gridIndex] = cell;
                            _lightSourceArray[gridIndex] = color24;
                        }
                    }
                }
            }
           
        }
        public void Dispose() {
            if(_lightMap!=null) {
                _lightMap.Dispose();
                _lightMap = null;
                _cellArray.Dispose();
                _diffsArray.Dispose();
                _lightSourceArray.Dispose();
            }
            
        }
        int CalculateCellIndex(int x, int y, int gridWidth)
        {
            return x + y * gridWidth;
        }
        void OnDestroy() {
            Dispose();
        }
    }
}