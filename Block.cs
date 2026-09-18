using Godot;

public partial class Block : Node3D
{

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }

    private MeshInstance3D _meshInstance;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _meshInstance = GetNode<MeshInstance3D>("MeshInstance3D");
    }

    public void SetColor(Color color)
    {
        // Creates a unique material copy so changing one block's color doesn't change them all
        var activeMat = _meshInstance.GetActiveMaterial(0);
        if (activeMat is StandardMaterial3D standardMat)
        {
            var mat = (StandardMaterial3D)standardMat.Duplicate();
            mat.AlbedoColor = color;
            _meshInstance.MaterialOverride = mat;
        }
    }
}
