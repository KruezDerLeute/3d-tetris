@tool
extends Node

func _ready():
	var shapes = {
		"icon_I.png": Color.CYAN,
		"icon_O.png": Color.YELLOW,
		"icon_T.png": Color.MAGENTA,
		"icon_L.png": Color.ORANGE,
		"icon_J.png": Color.BLUE,
		"icon_S.png": Color.GREEN,
		"icon_Z.png": Color.RED
	}
	
	for file_name in shapes:
		var img = Image.create(64, 64, false, Image.FORMAT_RGBA8)
		img.fill(shapes[file_name])
		img.save_png("res://" + file_name)
	print("Icons generated in res://!")
