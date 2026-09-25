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

    private List<Node3D> _activeBlockNodes = new(); // holds the actual 3d shape blocks
    private List<Node3D> _ghostBlockNodes = new();
    private Color _currentColor = Colors.Cyan;

    private Label _scoreLabel;
    private TextureRect _nextPieceTexture;
    private Control _gameOverPanel;
    private Button _restartButton;

    private int _score = 0;
    private readonly int[] _linePoints = { 0, 100, 300, 500, 800 };

    private float _dropTimer = 0.0f;
    private float _dropInterval = 1.5f;
    private bool _isGameOver = false;

    private string _nextShapeKey;
    private Color _nextColor;

    private Node3D _cameraPivot;
    private float _targetYRotation = Mathf.Pi / 4.0f; // 45 degrees initial rotation

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

        // Position pivot at exact center of the board: X=2, Y=9.5, Z=2
        _cameraPivot.Position = new Vector3(
            (BoardWidth - 1) / 2.0f,   // 2.0f
            (BoardHeight - 1) / 2.0f,  // 9.5f
            (BoardDepth - 1) / 2.0f    // 2.0f
        );

        // Apply starting camera rotation
        Vector3 startRot = _cameraPivot.Rotation;
        startRot.Y = 90;
        _cameraPivot.Rotation = startRot;

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

        // Camera Rotation Inputs (Q / E)
        if (Input.IsActionJustPressed("rotate_cam_left"))
            _targetYRotation += Mathf.Pi / 2.0f;
        else if (Input.IsActionJustPressed("rotate_cam_right"))
            _targetYRotation -= Mathf.Pi / 2.0f;

        // Smoothly rotate camera pivot
        Vector3 currentRot = _cameraPivot.Rotation;
        currentRot.Y = Mathf.LerpAngle(currentRot.Y, _targetYRotation, (float)delta * 10.0f);
        _cameraPivot.Rotation = currentRot;

        // --- Piece Movement Controls (Camera-Relative) ---
        if (Input.IsActionJustPressed("ui_left"))
            TryMove(GetCameraRelativeDirection(new Vector3I(-1, 0, 0))); // Left relative to view
        else if (Input.IsActionJustPressed("ui_right"))
            TryMove(GetCameraRelativeDirection(new Vector3I(1, 0, 0)));  // Right relative to view
        else if (Input.IsActionJustPressed("ui_up"))
            TryMove(GetCameraRelativeDirection(new Vector3I(0, 0, -1))); // Away from camera
        else if (Input.IsActionJustPressed("ui_down"))
            TryMove(GetCameraRelativeDirection(new Vector3I(0, 0, 1)));  // Towards camera
        else if (Input.IsActionJustPressed("ui_accept"))
            HardDrop();
        else if (Input.IsActionJustPressed("ui_select"))
            RotatePiece();
        else if (Input.IsActionJustPressed("ui_x"))
            RotatePieceX();
        else if (Input.IsActionJustPressed("ui_z"))
            RotatePieceZ();

        // Gravity drop timer
        _dropTimer += (float)delta;
        if (_dropTimer >= _dropInterval)
        {
            _dropTimer = 0.0f;
            TryMove(new Vector3I(0, -1, 0)); // Always drops down along Y-axis
        }
    }

    // --- 3D Rotation Methods ---

    private void RotatePiece()
    {
        // Y-Axis rotation (Yaw)
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

    private void RotatePieceX()
    {
        // X-Axis rotation (Pitch)
        List<Vector3I> rotatedBlocks = new();
        foreach (var b in _currentShapeBlocks)
        {
            rotatedBlocks.Add(new Vector3I(b.X, -b.Z, b.Y));
        }

        if (IsValidPosition(rotatedBlocks, _currentPos))
        {
            _currentShapeBlocks = rotatedBlocks;
            UpdatePiecePosition();
        }
    }

    private void RotatePieceZ()
    {
        // Z-Axis rotation (Roll)
        List<Vector3I> rotatedBlocks = new();
        foreach (var b in _currentShapeBlocks)
        {
            rotatedBlocks.Add(new Vector3I(-b.Y, b.X, b.Z));
        }

        if (IsValidPosition(rotatedBlocks, _currentPos))
        {
            _currentShapeBlocks = rotatedBlocks;
            UpdatePiecePosition();
        }
    }

    // --- Wireframe Setup ---

    private void CreateBoardWireframe()
    {
        var meshInstance = new MeshInstance3D();
        var immediateMesh = new ImmediateMesh();

        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 0.4f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        float minX = -0.5f, maxX = BoardWidth - 0.5f;
        float minY = -0.5f, maxY = BoardHeight - 0.5f;
        float minZ = -0.5f, maxZ = BoardDepth - 0.5f;

        immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);

        void AddLine(Vector3 start, Vector3 end)
        {
            immediateMesh.SurfaceAddVertex(start);
            immediateMesh.SurfaceAddVertex(end);
        }

        Vector3[] c = new Vector3[]
        {
            new(minX, minY, minZ), new(maxX, minY, minZ),
            new(maxX, minY, maxZ), new(minX, minY, maxZ),
            new(minX, maxY, minZ), new(maxX, maxY, minZ),
            new(maxX, maxY, maxZ), new(minX, maxY, maxZ)
        };

        AddLine(c[0], c[1]); AddLine(c[1], c[2]); AddLine(c[2], c[3]); AddLine(c[3], c[0]);
        AddLine(c[4], c[5]); AddLine(c[5], c[6]); AddLine(c[6], c[7]); AddLine(c[7], c[4]);
        AddLine(c[0], c[4]); AddLine(c[1], c[5]); AddLine(c[2], c[6]); AddLine(c[3], c[7]);

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

    // --- Clearing Logic (Full Layer + Outer Edges) ---

    private void CheckAndClearLayers()
    {
        int linesCleared = 0;

        for (int y = 0; y < BoardHeight; y++)
        {
            // 1. Check if full 5x5 layer is filled
            if (IsLayerFull(y))
            {
                ClearLayer(y);
                ShiftLayersDownAbove(y);
                linesCleared += 4;
                y--;
                continue;
            }

            // 2. Check individual 5-block outer edge lines
            bool clearedAnyEdge = false;

            if (IsLineFullX(y, 0)) // Back Edge
            {
                ClearLineX(y, 0);
                ShiftColumnLineDownX(y, 0);
                linesCleared++;
                clearedAnyEdge = true;
            }
            if (IsLineFullX(y, BoardDepth - 1)) // Front Edge
            {
                ClearLineX(y, BoardDepth - 1);
                ShiftColumnLineDownX(y, BoardDepth - 1);
                linesCleared++;
                clearedAnyEdge = true;
            }
            if (IsLineFullZ(y, 0)) // Left Edge
            {
                ClearLineZ(y, 0);
                ShiftColumnLineDownZ(y, 0);
                linesCleared++;
                clearedAnyEdge = true;
            }
            if (IsLineFullZ(y, BoardWidth - 1)) // Right Edge
            {
                ClearLineZ(y, BoardWidth - 1);
                ShiftColumnLineDownZ(y, BoardWidth - 1);
                linesCleared++;
                clearedAnyEdge = true;
            }

            if (clearedAnyEdge)
            {
                y--;
            }
        }

        if (linesCleared > 0)
        {
            _score += _linePoints[Mathf.Min(linesCleared, 4)];
            UpdateScoreUI();
        }
    }

    // --- Full Layer Helpers ---

    private bool IsLayerFull(int y)
    {
        for (int x = 0; x < BoardWidth; x++)
            for (int z = 0; z < BoardDepth; z++)
                if (_grid[x, y, z] == null) return false;
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
                        _grid[x, y, z].Position = new Vector3(x, y, z);
                    _grid[x, y + 1, z] = null;
                }
            }
        }
    }

    // --- Outer Line Check Helpers ---

    private bool IsLineFullX(int y, int z)
    {
        for (int x = 0; x < BoardWidth; x++)
        {
            if (_grid[x, y, z] == null) return false;
        }
        return true;
    }

    private bool IsLineFullZ(int y, int x)
    {
        for (int z = 0; z < BoardDepth; z++)
        {
            if (_grid[x, y, z] == null) return false;
        }
        return true;
    }

    private void ClearLineX(int y, int z)
    {
        for (int x = 0; x < BoardWidth; x++)
        {
            if (_grid[x, y, z] != null)
            {
                _grid[x, y, z].QueueFree();
                _grid[x, y, z] = null;
            }
        }
    }

    private void ClearLineZ(int y, int x)
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

    private void ShiftColumnLineDownX(int clearedY, int z)
    {
        for (int y = clearedY; y < BoardHeight - 1; y++)
        {
            for (int x = 0; x < BoardWidth; x++)
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

    private void ShiftColumnLineDownZ(int clearedY, int x)
    {
        for (int y = clearedY; y < BoardHeight - 1; y++)
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

    // --- Camera & Movement Helpers ---

    private Vector3I GetCameraRelativeDirection(Vector3I localInput)
    {
        int step = Mathf.PosMod(Mathf.RoundToInt(_targetYRotation / (Mathf.Pi / 2.0f)), 4);

        return step switch
        {
            0 => localInput,
            1 => new Vector3I(localInput.Z, localInput.Y, -localInput.X),
            2 => new Vector3I(-localInput.X, localInput.Y, -localInput.Z),
            3 => new Vector3I(-localInput.Z, localInput.Y, localInput.X),
            _ => localInput
        };
    }

    private bool IsValidPosition(List<Vector3I> blocks, Vector3I gridPos)
    {
        foreach (var b in blocks)
        {
            Vector3I target = gridPos + b;

            // checking if the block is out of bounds
            if (target.X < 0 || target.X >= BoardWidth ||
                target.Z < 0 || target.Z >= BoardDepth ||
                target.Y < 0)
                return false;

            // checking if the block is above the board height and the target placement is null
            // game over condition
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

        SpawnGhostPiece(); // setting the ghostbody positions on the game board
        UpdatePiecePosition(); // setting the position on the game board 
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
            blockInstance.SetColor(new Color(_currentColor).Darkened(0.5f));
            _ghostBlockNodes.Add(blockInstance);
        }
    }

    private void UpdateGhostPosition()
    {
        Vector3I ghostPos = _currentPos;
        //checking if every unit is allowed to render ghost body
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

        while (IsValidPosition(_currentShapeBlocks, _currentPos + new Vector3I(0, -1, 0)))
        {
            _currentPos += new Vector3I(0, -1, 0);
            dropDistance++;
        }

        UpdatePiecePosition();
        _score += dropDistance * 2;
        UpdateScoreUI();
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

