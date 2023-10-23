class_name GUI extends Control



func _setup(VIEW_DISTANCE: float, SEPERATION_DISTANCE: float, MOVEMENT_SPEED: float, COHESION: float, ALIGNMENT: float, SEPERATION: float):
	$View_Distance.get_child(1).text = str(VIEW_DISTANCE);
	$View_Distance.value = VIEW_DISTANCE;
	
	$Seperation_Distance.get_child(1).text = str(SEPERATION_DISTANCE);
	$Seperation_Distance.value = SEPERATION_DISTANCE;
	
	$Move_Speed.get_child(1).text = str(MOVEMENT_SPEED);
	$Move_Speed.value = MOVEMENT_SPEED;
	
	$Cohesion.get_child(1).text = str(COHESION);
	$Cohesion.value = COHESION;
	
	$Alignment.get_child(1).text = str(ALIGNMENT);
	$Alignment.value = ALIGNMENT;
	
	$Seperation.get_child(1).text = str(SEPERATION);
	$Seperation.value = SEPERATION;
# End Function


func _on_view_distance_value_changed(value: float) -> void:
	$View_Distance.get_child(1).text = str(value);
	get_parent().VISUAL_RANGE = value;
# End Function


func _on_seperation_distance_value_changed(value: float) -> void:
	$Seperation_Distance.get_child(1).text = str(value);
	get_parent().SEPERATION_DISTANCE = value;
# End Function


func _on_move_speed_value_changed(value: float) -> void:
	$Move_Speed.get_child(1).text = str(value);
	get_parent().MOVEMENT_SPEED = value;
# End Function


func _on_cohesion_value_changed(value: float) -> void:
	$Cohesion.get_child(1).text = str(value);
	get_parent().COHESION = value;
# End Function


func _on_alignment_value_changed(value: float) -> void:
	$Alignment.get_child(1).text = str(value);
	get_parent().ALIGNMENT = value;
# End Function


func _on_seperation_value_changed(value: float) -> void:
	$Seperation.get_child(1).text = str(value);
	get_parent().SEPERATION = value;
# End Function


func _on_boundry_button_toggled(boolean: bool) -> void:
	$TopLine.visible = boolean;
	$LeftLine.visible = boolean
	$BottomLine.visible = boolean
	$RightLine.visible = boolean
	
	var parent = get_parent();
	parent.BOUNDRY_ENABLED = boolean;
	
	if(boolean):
		parent.boundryBool = 1;
	else:
		parent.boundryBool = 0;
	# End If/ Else
# End Function


func _setupBoundryLines(margin: float, screen: Vector2):
	var boundryVisible = get_parent().BOUNDRY_ENABLED;
	
	$TopLine.points = PackedVector2Array([Vector2(margin, margin), Vector2(screen.x - margin, margin)]);
	$LeftLine.points = PackedVector2Array([Vector2(margin, margin), Vector2(margin, screen.y - margin)]);
	$BottomLine.points = PackedVector2Array([Vector2(margin, screen.y- margin), Vector2(screen.x - margin, screen.y - margin)]);
	$RightLine.points = PackedVector2Array([Vector2(screen.x - margin, margin), Vector2(screen.x - margin, screen.y - margin)]);
	
	$TopLine.visible = boundryVisible;
	$LeftLine.visible = boundryVisible;
	$BottomLine.visible = boundryVisible;
	$RightLine.visible = boundryVisible;
