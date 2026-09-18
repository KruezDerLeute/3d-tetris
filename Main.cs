using Godot;
using System.Collections.Generic;

public partial class Main : Node3D
{
    [Export] public PackedScene BlockScene;

    private const int BoardWidth = 10;
    private const int BoardHeight = 20;

    private Node3D[,] _grid = new Node3D[BoardWidth, BoardHeight];

    private List<Vector2I> _currentShapeBlocks = new();
    private Vector2I _currentPos = new(4, 18);
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

    private readonly Dictionary<string, Color> _shapeColors = new()
    {
        { "I", Colors.Cyan },
        { "O", Colors.Yellow },
        { "T", Colors.Magenta },
        { "L", Colors.Orange },
        { "J", Colors.Blue },
        { "S", Colors.Green },
        { "Z", Colors.Red }
    };

    private readonly Dictionary<string, Texture2D> _shapeTextures = new();

    private readonly Dictionary<string, List<Vector2I>> _shapes = new()
    {
        { "I", new List<Vector2I> { new(0, -1), new(0, 0), new(0, 1), new(0, 2) } },
        { "O", new List<Vector2I> { new(0, 0), new(1, 0), new(0, 1), new(1, 1) } },
        { "T", new List<Vector2I> { new(-1, 0), new(0, 0), new(1, 0), new(0, 1) } },
        { "L", new List<Vector2I> { new(0, -1), new(0, 0), new(0, 1), new(1, 1) } },
        { "J", new List<Vector2I> { new(0, -1), new(0, 0), new(0, 1), new(-1, 1) } },
        { "S", new List<Vector2I> { new(-1, 0), new(0, 0), new(0, 1), new(1, 1) } },
        { "Z", new List<Vector2I> { new(1, 0), new(0, 0), new(0, 1), new(-1, 1) } }
    };

    public override void _Ready()
    {
        _scoreLabel = GetNode<Label>("CanvasLayer/ScoreLabel");
        _nextPieceTexture = GetNode<TextureRect>("CanvasLayer/NextPieceTexture");

        // Game Over UI nodes
        _gameOverPanel = GetNode<Control>("CanvasLayer/GameOverPanel");
        _restartButton = GetNode<Button>("CanvasLayer/GameOverPanel/RestartButton");
        _restartButton.Pressed += OnRestartButtonPressed;
        _gameOverPanel.Hide();

        // Load preview images
        _shapeTextures["I"] = GD.Load<Texture2D>("res://icon_I.png");
        _shapeTextures["O"] = GD.Load<Texture2D>("res://icon_O.png");
        _shapeTextures["T"] = GD.Load<Texture2D>("res://icon_T.png");
        _shapeTextures["L"] = GD.Load<Texture2D>("res://icon_L.png");
        _shapeTextures["J"] = GD.Load<Texture2D>("res://icon_J.png");
        _shapeTextures["S"] = GD.Load<Texture2D>("res://icon_S.png");
        _shapeTextures["Z"] = GD.Load<Texture2D>("res://icon_Z.png");

        UpdateScoreUI();

        // Roll first piece and queued next piece
        var nextPiece = GetRandomPiece();
        _nextShapeKey = nextPiece.key;
        _nextColor = nextPiece.color;

        var firstPiece = GetRandomPiece();
        SpawnPiece(firstPiece.key, firstPiece.color);
        UpdateNextPieceUI();
    }

    public override void _Process(double delta)
    {
        if (_isGameOver) return;

        _dropTimer += (float)delta;
        if (_dropTimer >= _dropInterval)
        {
            _dropTimer = 0.0f;
            TryMove(new Vector2I(0, -1));
        }

        if (Input.IsActionJustPressed("ui_left"))
            TryMove(new Vector2I(-1, 0));
        else if (Input.IsActionJustPressed("ui_right"))
            TryMove(new Vector2I(1, 0));
        else if (Input.IsActionJustPressed("ui_down"))
            TryMove(new Vector2I(0, -1));
        else if (Input.IsActionJustPressed("ui_up"))
            RotatePiece();
    }

    private (string key, Color color) GetRandomPiece()
    {
        string[] shapeKeys = { "I", "O", "T", "L", "J", "S", "Z" };
        int randomIndex = (int)(GD.Randi() % shapeKeys.Length);
        string selectedKey = shapeKeys[randomIndex];
        return (selectedKey, _shapeColors[selectedKey]);
    }

    private bool TryMove(Vector2I delta)
    {
        Vector2I newPos = _currentPos + delta;
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
        List<Vector2I> rotatedBlocks = new();
        foreach (var b in _currentShapeBlocks)
        {
            rotatedBlocks.Add(new Vector2I(-b.Y, b.X));
        }

        if (IsValidPosition(rotatedBlocks, _currentPos))
        {
            _currentShapeBlocks = rotatedBlocks;
            UpdatePiecePosition();
        }
    }

    private bool IsValidPosition(List<Vector2I> blocks, Vector2I gridPos)
    {
        foreach (var b in blocks)
        {
            Vector2I target = gridPos + b;
            if (target.X < 0 || target.X >= BoardWidth || target.Y < 0)
                return false;
            if (target.Y < BoardHeight && _grid[target.X, target.Y] != null)
                return false;
        }
        return true;
    }

    private void LockPiece()
    {
        for (int i = 0; i < _currentShapeBlocks.Count; i++)
        {
            Vector2I pos = _currentPos + _currentShapeBlocks[i];
            if (pos.Y >= 0 && pos.Y < BoardHeight && pos.X >= 0 && pos.X < BoardWidth)
            {
                _grid[pos.X, pos.Y] = _activeBlockNodes[i];
            }
        }
        _activeBlockNodes.Clear();

        // Clear ghost piece nodes when locking
        foreach (var node in _ghostBlockNodes) node.QueueFree();
        _ghostBlockNodes.Clear();

        CheckAndClearLines();

        // Spawn queued piece and roll next piece
        SpawnPiece(_nextShapeKey, _nextColor);

        var newNext = GetRandomPiece();
        _nextShapeKey = newNext.key;
        _nextColor = newNext.color;
        UpdateNextPieceUI();
    }

    private void CheckAndClearLines()
    {
        int linesCleared = 0;

        for (int y = 0; y < BoardHeight; y++)
        {
            if (IsLineFull(y))
            {
                ClearLine(y);
                ShiftLinesDownAbove(y);
                linesCleared++;
                y--;
            }
        }

        if (linesCleared > 0)
        {
            _score += _linePoints[linesCleared];
            UpdateScoreUI();
        }
    }

    private bool IsLineFull(int y)
    {
        for (int x = 0; x < BoardWidth; x++)
        {
            if (_grid[x, y] == null)
                return false;
        }
        return true;
    }

    private void ClearLine(int y)
    {
        for (int x = 0; x < BoardWidth; x++)
        {
            if (_grid[x, y] != null)
            {
                _grid[x, y].QueueFree();
                _grid[x, y] = null;
            }
        }
    }

    private void ShiftLinesDownAbove(int clearedY)
    {
        for (int y = clearedY; y < BoardHeight - 1; y++)
        {
            for (int x = 0; x < BoardWidth; x++)
            {
                _grid[x, y] = _grid[x, y + 1];

                if (_grid[x, y] != null)
                {
                    _grid[x, y].Position = new Vector3(x, y, 0);
                }

                _grid[x, y + 1] = null;
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
        _currentShapeBlocks = new List<Vector2I>(_shapes[shapeKey]);
        _currentPos = new Vector2I(4, 18);
        _currentColor = color;

        // Trigger game over if spawn area is blocked
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
            Vector2I gridCoord = _currentPos + _currentShapeBlocks[i];
            _activeBlockNodes[i].Position = new Vector3(gridCoord.X, gridCoord.Y, 0);
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
        Vector2I ghostPos = _currentPos;
        while (IsValidPosition(_currentShapeBlocks, ghostPos + new Vector2I(0, -1)))
        {
            ghostPos += new Vector2I(0, -1);
        }

        for (int i = 0; i < _currentShapeBlocks.Count; i++)
        {
            Vector2I gridCoord = ghostPos + _currentShapeBlocks[i];
            _ghostBlockNodes[i].Position = new Vector3(gridCoord.X, gridCoord.Y, 0);
        }
    }

    private void TriggerGameOver()
    {
        _isGameOver = true;
        _gameOverPanel.Show();
    }

    private void OnRestartButtonPressed()
    {
        GetTree().ReloadCurrentScene();
    }
}
