"""Check that both face families retain their contracts and eye states stay local."""
from pathlib import Path
import bpy

root=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/FestivalCharacter.blend'))
allowed={'IntoxSclera','IntoxIris','IntoxPupil','IntoxCatchlight','IntoxUpperLid','IntoxLowerLid'}
for gender in range(2):
    for face in range(6):
        obj=bpy.data.objects[f'Face_{gender}_{face}']
        assert [m.name for m in obj.data.materials]==['FestivalPalette','FestivalEyeWhite']
        keys=obj.data.shape_keys.key_blocks
        assert 'Blink' in keys and 'WideIntoxicatedEyes' in keys
        changed=0
        for vertex in obj.data.vertices:
            delta=(keys['WideIntoxicatedEyes'].data[vertex.index].co-keys['Basis'].data[vertex.index].co).length
            if delta<.00001:continue
            groups={obj.vertex_groups[g.group].name for g in vertex.groups}
            assert groups & allowed,(obj.name,vertex.index,groups)
            changed+=1
        assert changed>300,(obj.name,changed)
print('FACIAL STATE AUDIT PASSED: 12 faces, both styles, eye-only wide morph, separate eye material')
