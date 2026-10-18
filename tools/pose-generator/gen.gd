# Pose generator: an editor script for the Godot MCP (`editor exec`), not a part of the game.
# generate.sh pastes a pose table into the POSES variable below, opens Fighter01.tscn, runs this script, and saves the scene.
# For each animation in the table, it REPLACES the animation in the AnimationPlayer library with bone tracks only
# (hip position and rotation, all bone rotations): hitbox and hurtbox tracks of that animation are lost. Do not use it
# on an animation with box tracks (attacks) without adding the box tracks again.
# It also adds missing bone tracks (the rest pose) to RESET, checks each key pose (the feet, hands, and head positions
# are printed), and restores the exact bone values of the scene after the check.
var r = EditorInterface.get_edited_scene_root()
if r.scene_file_path != 'res://scenes/fighters/Fighter01.tscn':
	_mcp_print('wrong scene ' + r.scene_file_path)
	return
var B = 'StickFigure/Skeleton2D/Hip'
var P = {
	'Hip': B, 'Spine': B + '/Spine', 'Neck': B + '/Spine/Neck',
	'UpperArmNear': B + '/Spine/UpperArmNear', 'ForearmNear': B + '/Spine/UpperArmNear/ForearmNear',
	'UpperArmFar': B + '/Spine/UpperArmFar', 'ForearmFar': B + '/Spine/UpperArmFar/ForearmFar',
	'ThighNear': B + '/ThighNear', 'ShinNear': B + '/ThighNear/ShinNear',
	'ThighFar': B + '/ThighFar', 'ShinFar': B + '/ThighFar/ShinFar',
}
# Two-bone IK: global angles of the upper and lower bone that reach the target.
# side = -1: the middle joint bends to the smaller angle (knees forward); +1: to the larger angle (elbows down/back).
var ik = func(origin: Vector2, target: Vector2, l1: float, l2: float, side: float) -> Array:
	var d = target - origin
	var dist = clampf(d.length(), absf(l1 - l2) + 0.01, l1 + l2 - 0.01)
	var a = atan2(d.y, d.x)
	var c = clampf((l1 * l1 + dist * dist - l2 * l2) / (2.0 * l1 * dist), -1.0, 1.0)
	var a1 = a + side * acos(c)
	var mid = origin + Vector2(cos(a1), sin(a1)) * l1
	var a2 = (target - mid).angle()
	return [a1, a2]
# A pose: hip position, spine and neck angles (degrees, global; -90 = up), feet and hands (relative to the feet origin).
# Returns bone name -> local rotation (radians), plus 'HipPosition'.
var solve = func(hip: Vector2, spine_deg: float, neck_deg: float, foot_near: Vector2, foot_far: Vector2, hand_near, hand_far) -> Dictionary:
	var spine = deg_to_rad(spine_deg)
	var neck = deg_to_rad(neck_deg)
	var chest = hip + Vector2(cos(spine), sin(spine)) * 25.0
	var leg_near = ik.call(hip, foot_near, 20.0, 20.0, -1.0)
	var leg_far = ik.call(hip, foot_far, 20.0, 20.0, -1.0)
	# A hand is a target point (Vector2, solved with IK) or [upper arm, forearm] global angles in degrees.
	var arm_near = [deg_to_rad(hand_near[0]), deg_to_rad(hand_near[1])] if hand_near is Array else ik.call(chest, hand_near, 17.0, 16.0, 1.0)
	var arm_far = [deg_to_rad(hand_far[0]), deg_to_rad(hand_far[1])] if hand_far is Array else ik.call(chest, hand_far, 17.0, 16.0, 1.0)
	return {
		'HipPosition': hip,
		'Hip': 0.0,
		'Spine': spine,
		'Neck': wrapf(neck - spine, -PI, PI),
		'ThighNear': leg_near[0], 'ShinNear': wrapf(leg_near[1] - leg_near[0], -PI, PI),
		'ThighFar': leg_far[0], 'ShinFar': wrapf(leg_far[1] - leg_far[0], -PI, PI),
		'UpperArmNear': wrapf(arm_near[0] - spine, -PI, PI), 'ForearmNear': wrapf(arm_near[1] - arm_near[0], -PI, PI),
		'UpperArmFar': wrapf(arm_far[0] - spine, -PI, PI), 'ForearmFar': wrapf(arm_far[1] - arm_far[0], -PI, PI),
	}
# Makes an animation from (frame, pose) keys. All bones are keyed in each animation.
var make = func(frames: int, loop: bool, keys: Array) -> Animation:
	var anim = Animation.new()
	anim.length = frames / 60.0
	anim.step = 1.0 / 60.0
	anim.loop_mode = Animation.LOOP_LINEAR if loop else Animation.LOOP_NONE
	var hp = anim.add_track(Animation.TYPE_VALUE)
	anim.track_set_path(hp, P['Hip'] + ':position')
	anim.track_set_interpolation_type(hp, Animation.INTERPOLATION_LINEAR)
	for k in keys:
		anim.track_insert_key(hp, k[0] / 60.0, k[1]['HipPosition'])
	for bone in P.keys():
		var t = anim.add_track(Animation.TYPE_VALUE)
		anim.track_set_path(t, P[bone] + ':rotation')
		anim.track_set_interpolation_type(t, Animation.INTERPOLATION_LINEAR_ANGLE)
		for k in keys:
			anim.track_insert_key(t, k[0] / 60.0, k[1][bone])
	return anim
# RESET: the rest pose of the skeleton (the values in the scene).
var rest = {'HipPosition': r.get_node(P['Hip']).position}
for bone in P.keys():
	rest[bone] = r.get_node(P[bone]).rotation
# POSES: name -> [frames, loop, [[frame, [hip, spine, neck, footNear, footFar, handNear, handFar]], ...]]
var POSES = __POSES__
var player = r.get_node('AnimationPlayer')
var lib = player.get_animation_library('')
var reset = lib.get_animation('RESET')
# Add the bone tracks to RESET (keep its hitbox tracks).
var reset_bones = make.call(1, false, [[0, rest]])
for t in reset_bones.get_track_count():
	var path = reset_bones.track_get_path(t)
	if reset.find_track(path, Animation.TYPE_VALUE) == -1:
		var nt = reset.add_track(Animation.TYPE_VALUE)
		reset.track_set_path(nt, path)
		reset.track_set_interpolation_type(nt, reset_bones.track_get_interpolation_type(t))
		reset.track_insert_key(nt, 0.0, reset_bones.track_get_key_value(t, 0))
var solved = {}
for n in POSES.keys():
	var spec = POSES[n]
	var keys = []
	for k in spec[2]:
		var p = k[1]
		var pose = solve.call(p[0], p[1], p[2], p[3], p[4], p[5], p[6])
		keys.append([k[0], pose])
		solved[n + str(k[0])] = pose
	if lib.has_animation(n):
		lib.remove_animation(n)
	var anim_made = make.call(spec[0], spec[1], keys)
	# Optional 4th value: the frame of the "loop_start" marker (intro before it, loop after it).
	if spec.size() > 3:
		anim_made.add_marker('loop_start', spec[3] / 60.0)
	lib.add_animation(n, anim_made)
# Check: apply each walk key pose to the bones and measure the feet and hands.
var figure = r.get_node('StickFigure')
var tip = func(name: String) -> Vector2:
	var b = r.get_node(P[name])
	return figure.to_local(b.global_transform * Vector2(b.get_length(), 0)).round()
# Keep the exact scene values of the bones: the check below changes them, and they are restored after it.
var saved = {}
for b in r.find_children('*', 'Bone2D', true, false):
	saved[b] = [b.position, b.rotation, b.scale]
var out = []
for key in solved.keys():
	var pose = solved[key]
	r.get_node(P['Hip']).position = pose['HipPosition']
	for bone in P.keys():
		r.get_node(P[bone]).rotation = pose[bone]
	out.append(key + ': footN ' + str(tip.call('ShinNear')) + ' footF ' + str(tip.call('ShinFar')) + ' handN ' + str(tip.call('ForearmNear')) + ' handF ' + str(tip.call('ForearmFar')) + ' head ' + str(tip.call('Neck')))
# Back to the exact scene values (rotation first: it can change the scale).
for b in saved.keys():
	b.position = saved[b][0]
	b.rotation = saved[b][1]
	b.scale = saved[b][2]
out.append('animations ' + str(lib.get_animation_list()) + ' RESET tracks ' + str(reset.get_track_count()))
out.append('warnings ' + str(r._get_configuration_warnings()))
_mcp_print('\n'.join(out))
