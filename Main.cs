using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class Main : Node3D
{
    [Export] public PackedScene BlockScene;

    private const int BoardWidth = 5;
    private const int BoardHeight = 20;
    private const int BoardDepth = 5;

    // 3D Grid Array [X, Y, Z]
    private Node3D[,,] _grid = new Node3D[BoardWidth, BoardHeight, BoardDepth];

    private List<Vector3I> _currentShapeBlocks = new();
    private Vector3I _currentPos = new(2, 18, 2); // Spawns at top-center of a 5x20x5 grid
    private List<Node3D> _activeBlockNodes = new();
    private List<Node3D> _ghostBlockNodes = new();
    private Color _currentColor = Colors.Cyan;

    private Label _scoreLabel;
    private TextureRect _nextPieceTexture;
    private Control _gameOverPanel;
    private Button _restartButton;

    private int _score = 0;
    private readonly int[] _linePoints = { 0, 100, 300, 500, 800 };

    private float _dropTimer = 0.0f;
    private float _dropInterval = 0.8f;
    private bool _isGameOver = false;

    private string _nextShapeKey;
    private Color _nextColor;

    private Node3D _cameraPivot;
    private float _targetYRotation = 0.0f;

    private readonly Dictionary<string, Color> _shapeColors = new()
    {
        { "I", Colors.Cyan },
        { "O", Colors.Yellow },
        { "T", Colors.Magenta },
        { "L", Colors.Orange },
        { "J", Colors.Blue },
        { "S", Colors.Green },
        { "Z", Colors.Red },
        { "Corner3D", Colors.Purple },
        { "TwistL", Colors.Teal },
        { "TwistR", Colors.Pink },
        { "I3", Colors.White },
        { "L3", Colors.Lime },
        { "Corner3_3D", Colors.Coral }
    };

    private readonly Dictionary<string, Texture2D> _shapeTextures = new();

    private readonly Dictionary<string, List<Vector3I>> _shapes = new()
    {
        { "I", new List<Vector3I> { new(0, -1, 0), new(0, 0, 0), new(0, 1, 0), new(0, 2, 0) } },
        { "O", new List<Vector3I> { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 0) } },
        { "T", new List<Vector3I> { new(-1, 0, 0), new(0, 0, 0), new(1, 0, 0), new(0, 1, 0) } },
        { "L", new List<Vector3I> { new(0, -1, 0), new(0, 0, 0), new(0, 1, 0), new(1, -1, 0) } },
        { "J", new List<Vector3I> { new(0, -1, 0), new(0, 0, 0), new(0, 1, 0), new(-1, -1, 0) } },
        { "S", new List<Vector3I> { new(-1, 0, 0), new(0, 0, 0), new(0, 1, 0), new(1, 1, 0) } },
        { "Z", new List<Vector3I> { new(1, 0, 0), new(0, 0, 0), new(0, 1, 0), new(-1, 1, 0) } },
        { "Corner3D", new List<Vector3I> { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1) } },
        { "TwistL", new List<Vector3I> { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 0, 1) } },
        { "TwistR", new List<Vector3I> { new(0, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(-1, 0, 1) } },
        { "I3", new List<Vector3I> { new(0, -1, 0), new(0, 0, 0), new(0, 1, 0) } },
        { "L3", new List<Vector3I> { new(0, 0, 0), new(1, 0, 0), new(0, 1, 0) } },
        { "Corner3_3D", new List<Vector3I> { new(0, 0, 0), new(1, 0, 0), new(0, 0, 1) } }
    };

    public override void _Ready()
    {
        _scoreLabel = GetNodeOrNull<Label>("CanvasLayer/ScoreLabel");
        _nextPieceTexture = GetNodeOrNull<TextureRect>("CanvasLayer/NextPieceTexture");

        _gameOverPanel = GetNode<Control>("CanvasLayer/GameOverPanel");
        _restartButton = GetNode<Button>("CanvasLayer/GameOverPanel/RestartButton");
        _restartButton.Pressed += OnRestartButtonPressed;
        _gameOverPanel.Hide();

        _shapeTextures["I"] = GD.Load<Texture2D>("res://icon_I.png");
        _shapeTextures["O"] = GD.Load<Texture2D>("res://icon_O.png");
        _shapeTextures["T"] = GD.Load<Texture2D>("res://icon_T.png");
        _shapeTextures["L"] = GD.Load<Texture2D>("res://icon_L.png");
        _shapeTextures["J"] = GD.Load<Texture2D>("res://icon_J.png");
        _shapeTextures["S"] = GD.Load<Texture2D>("res://icon_S.png");
        _shapeTextures["Z"] = GD.Load<Texture2D>("res://icon_Z.png");

        _cameraPivot = GetNode<Node3D>("CameraPivot");

        UpdateScoreUI();

        var nextPiece = GetRandomPiece();
        _nextShapeKey = nextPiece.key;
        _nextColor = nextPiece.color;

        var firstPiece = GetRandomPiece();
        SpawnPiece(firstPiece.key, firstPiece.color);
        UpdateNextPieceUI();
        CreateBoardWireframe();
    }

    public override void _Process(double delta)
    {
        if (_isGameOver) return;

        _dropTimer += (float)delta;
        if (_dropTimer >= _dropInterval)
        {
            _dropTimer = 0.0f;
            TryMove(new Vector3I(0, -1, 0)); // Drop down Y-axis
        }

        // X and Z movement controls
        if (Input.IsActionJustPressed("ui_left"))
            TryMove(new Vector3I(-1, 0, 0));
        if (Input.IsActionJustPressed("hard_down")) HardDrop();
        else if (Input.IsActionJustPressed("ui_right"))
            TryMove(new Vector3I(1, 0, 0));
        else if (Input.IsActionJustPressed("ui_up"))
            TryMove(new Vector3I(0, 0, -1)); // Move backward along Z
        else if (Input.IsActionJustPressed("ui_down"))
            TryMove(new Vector3I(0, 0, 1));  // Move forward along Z
        else if (Input.IsActionJustPressed("ui_select")) // Spacebar or Custom key for Rotate
            RotatePiece();
    }

    private void CreateBoardWireframe()
    {
        var meshInstance = new MeshInstance3D();
        var immediateMesh = new ImmediateMesh();

        // Create an unshaded material so the box lines ignore scene lighting
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 0.4f), // Semi-transparent white
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        // Calculate boundary limits centered around the 1x1x1 grid blocks
        float minX = -0.5f, maxX = BoardWidth - 0.5f;
        float minY = -0.5f, maxY = BoardHeight - 0.5f;
        float minZ = -0.5f, maxZ = BoardDepth - 0.5f;

        // Begin drawing lines
        immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);

        void AddLine(Vector3 start, Vector3 end)
        {
            immediateMesh.SurfaceAddVertex(start);
            immediateMesh.SurfaceAddVertex(end);
        }

        // --- 1. Outer Bounding Box (12 Edges) ---
        Vector3[] c = new Vector3[]
        {
        new(minX, minY, minZ), new(maxX, minY, minZ),
        new(maxX, minY, maxZ), new(minX, minY, maxZ),
        new(minX, maxY, minZ), new(maxX, maxY, minZ),
        new(maxX, maxY, maxZ), new(minX, maxY, maxZ)
        };

        // Bottom rectangle
        AddLine(c[0], c[1]); AddLine(c[1], c[2]); AddLine(c[2], c[3]); AddLine(c[3], c[0]);
        // Top rectangle
        AddLine(c[4], c[5]); AddLine(c[5], c[6]); AddLine(c[6], c[7]); AddLine(c[7], c[4]);
        // Vertical corner pillars
        AddLine(c[0], c[4]); AddLine(c[1], c[5]); AddLine(c[2], c[6]); AddLine(c[3], c[7]);

        // --- 2. Bottom Floor Grid Lines (Helps judge where pieces land) ---
        for (int x = 1; x < BoardWidth; x++)
        {
            float xPos = x - 0.5f;
            AddLine(new Vector3(xPos, minY, minZ), new Vector3(xPos, minY, maxZ));
        }
        for (int z = 1; z < BoardDepth; z++)
        {
            float zPos = z - 0.5f;
            AddLine(new Vector3(minX, minY, zPos), new Vector3(maxX, minY, zPos));
        }

        immediateMesh.SurfaceEnd();

        meshInstance.Mesh = immediateMesh;
        AddChild(meshInstance);
    }

    private (string key, Color color) GetRandomPiece()
    {
        string[] keys = _shapes.Keys.ToArray();
        int randomIndex = (int)(GD.Randi() % keys.Length);
        string selectedKey = keys[randomIndex];
        return (selectedKey, _shapeColors[selectedKey]);
    }

    private bool TryMove(Vector3I delta)
    {
        Vector3I newPos = _currentPos + delta;
        if (IsValidPosition(_currentShapeBlocks, newPos))
        {
            _currentPos = newPos;
            UpdatePiecePosition();
            return true;
        }

        if (delta.Y < 0)
        {
            LockPiece();
        }
        return false;
    }

    private void RotatePiece()
    {
        // Y-Axis rotation (Yaw): (x, y, z) -> (-z, y, x)
        List<Vector3I> rotatedBlocks = new();
        foreach (var b in _currentShapeBlocks)
        {
            rotatedBlocks.Add(new Vector3I(-b.Z, b.Y, b.X));
        }

        if (IsValidPosition(rotatedBlocks, _currentPos))
        {
            _currentShapeBlocks = rotatedBlocks;
            UpdatePiecePosition();
        }
    }

    private bool IsValidPosition(List<Vector3I> blocks, Vector3I gridPos)
    {
        foreach (var b in blocks)
        {
            Vector3I target = gridPos + b;

            // Check 3D boundary limits (Walls, Floor, Depth)
            if (target.X < 0 || target.X >= BoardWidth ||
                target.Z < 0 || target.Z >= BoardDepth ||
                target.Y < 0)
                return false;

            // Check if cell is occupied by locked block
            if (target.Y < BoardHeight && _grid[target.X, target.Y, target.Z] != null)
                return false;
        }
        return true;
    }

    private void LockPiece()
    {
        for (int i = 0; i < _currentShapeBlocks.Count; i++)
        {
            Vector3I pos = _currentPos + _currentShapeBlocks[i];
            if (pos.Y >= 0 && pos.Y < BoardHeight &&
                pos.X >= 0 && pos.X < BoardWidth &&
                pos.Z >= 0 && pos.Z < BoardDepth)
            {
                _grid[pos.X, pos.Y, pos.Z] = _activeBlockNodes[i];
            }
        }
        _activeBlockNodes.Clear();

        foreach (var node in _ghostBlockNodes) node.QueueFree();
        _ghostBlockNodes.Clear();

        CheckAndClearLayers();

        SpawnPiece(_nextShapeKey, _nextColor);

        var newNext = GetRandomPiece();
        _nextShapeKey = newNext.key;
        _nextColor = newNext.color;
        UpdateNextPieceUI();
    }

    private void CheckAndClearLayers()
    {
        int layersCleared = 0;

        for (int y = 0; y < BoardHeight; y++)
        {
            if (IsLayerFull(y))
            {
                ClearLayer(y);
                ShiftLayersDownAbove(y);
                layersCleared++;
                y--;
            }
        }

        if (layersCleared > 0)
        {
            _score += _linePoints[Mathf.Min(layersCleared, 4)];
            UpdateScoreUI();
        }
    }

    private bool IsLayerFull(int y)
    {
        for (int x = 0; x < BoardWidth; x++)
        {
            for (int z = 0; z < BoardDepth; z++)
            {
                if (_grid[x, y, z] == null)
                    return false;
            }
        }
        return true;
    }

    private void ClearLayer(int y)
    {
        for (int x = 0; x < BoardWidth; x++)
        {
            for (int z = 0; z < BoardDepth; z++)
            {
                if (_grid[x, y, z] != null)
                {
                    _grid[x, y, z].QueueFree();
                    _grid[x, y, z] = null;
                }
            }
        }
    }

    private void ShiftLayersDownAbove(int clearedY)
    {
        for (int y = clearedY; y < BoardHeight - 1; y++)
        {
            for (int x = 0; x < BoardWidth; x++)
            {
                for (int z = 0; z < BoardDepth; z++)
                {
                    _grid[x, y, z] = _grid[x, y + 1, z];

                    if (_grid[x, y, z] != null)
                    {
                        _grid[x, y, z].Position = new Vector3(x, y, z);
                    }

                    _grid[x, y + 1, z] = null;
                }
            }
        }
    }

    private void UpdateScoreUI()
    {
        if (_scoreLabel != null)
        {
            _scoreLabel.Text = $"Score: {_score}";
        }
    }

    private void UpdateNextPieceUI()
    {
        if (_nextPieceTexture != null && _shapeTextures.ContainsKey(_nextShapeKey))
        {
            _nextPieceTexture.Texture = _shapeTextures[_nextShapeKey];
        }
    }

    private void SpawnPiece(string shapeKey, Color color)
    {
        // Updated to Vector3I list copying
        _currentShapeBlocks = new List<Vector3I>(_shapes[shapeKey]);
        _currentPos = new Vector3I(2, 18, 2);
        _currentColor = color;

        if (!IsValidPosition(_currentShapeBlocks, _currentPos))
        {
            TriggerGameOver();
            return;
        }

        foreach (var node in _activeBlockNodes)
        {
            node.QueueFree();
        }
        _activeBlockNodes.Clear();

        foreach (var offset in _currentShapeBlocks)
        {
            var blockInstance = BlockScene.Instantiate<Block>();
            AddChild(blockInstance);
            blockInstance.SetColor(_currentColor);
            _activeBlockNodes.Add(blockInstance);
        }

        SpawnGhostPiece();
        UpdatePiecePosition();
    }

    private void UpdatePiecePosition()
    {
        for (int i = 0; i < _currentShapeBlocks.Count; i++)
        {
            Vector3I gridCoord = _currentPos + _currentShapeBlocks[i];
            _activeBlockNodes[i].Position = new Vector3(gridCoord.X, gridCoord.Y, gridCoord.Z);
        }

        UpdateGhostPosition();
    }

    private void SpawnGhostPiece()
    {
        foreach (var node in _ghostBlockNodes) node.QueueFree();
        _ghostBlockNodes.Clear();

        foreach (var offset in _currentShapeBlocks)
        {
            var blockInstance = BlockScene.Instantiate<Block>();
            AddChild(blockInstance);
            blockInstance.SetColor(new Color(1, 1, 1, 0.3f));
            _ghostBlockNodes.Add(blockInstance);
        }
    }

    private void UpdateGhostPosition()
    {
        Vector3I ghostPos = _currentPos;
        while (IsValidPosition(_currentShapeBlocks, ghostPos + new Vector3I(0, -1, 0)))
        {
            ghostPos += new Vector3I(0, -1, 0);
        }

        for (int i = 0; i < _currentShapeBlocks.Count; i++)
        {
            Vector3I gridCoord = ghostPos + _currentShapeBlocks[i];
            _ghostBlockNodes[i].Position = new Vector3(gridCoord.X, gridCoord.Y, gridCoord.Z);
        }
    }

    private void HardDrop()
    {
        int dropDistance = 0;

        // 1. Instantly calculate bottom position
        while (IsValidPosition(_currentShapeBlocks, _currentPos + new Vector3I(0, -1, 0)))
        {
            _currentPos += new Vector3I(0, -1, 0);
            dropDistance++;
        }

        // 2. Move 3D meshes visually to the target position BEFORE locking
        UpdatePiecePosition();

        // 3. Score bonus
        _score += dropDistance * 2;
        UpdateScoreUI();

        // 4. Lock instantly
        LockPiece();
    }

    private void TriggerGameOver()
    {
        _isGameOver = true;
        _gameOverPanel?.Show();
    }

    private void OnRestartButtonPressed()
    {
        GetTree().ReloadCurrentScene();
    }
}
