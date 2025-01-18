#參考 Blender 官方的 Operator File Export
bl_info = {
    "name": "Vertex Animation Texture Exporter",
    "blender": (3, 0, 0),
    "author": "Lilacsky",
    "category": "Import-Export",
    "location": "File > Import > Mesh",
    "support": "COMMUNITY"
}

RGB_CHANNEL_COUNT = 4
FONT_SIZE = 8

import bpy
import bmesh
import uuid
import numpy as np
import textwrap 
import struct
from bpy_extras.io_utils import ExportHelper
from bpy.props import StringProperty, BoolProperty
from bpy.types import Operator

class VertexAnimationTextureOperator(Operator, ExportHelper):
    bl_idname = "object.vat_exporter"
    bl_label = "Export"
    
    filename_ext = ".exr"
    filter_glob : StringProperty(
    subtype="FILE_PATH",
    default=f"*{filename_ext}",
    options={'HIDDEN'},
    maxlen=255,  # Max internal buffer length, longer would be clamped.
    )
    export_normal : BoolProperty(
        name="Export Packed Normal",
        description="Include vertex normals in the export",
        default = True
    )
    print_result : BoolProperty(
        name="Print result",
        description="Print converted result into console",
        default = False
    )
    
    width = 0
    height = 0
    vat_name = "VAT"
    
    def draw_tip_text(self, context, layout):
        #這裡待確認
        textToWarp = "請注意 Vertex Animation Texture 仰賴頂點的 Index，匯出的建議不要有 Split Vertex Normal 與 UV Seam，因為這樣會導致頂點數的變動"   
        FONT_SIZE = 8
        wrapp = textwrap.TextWrapper(context.region.width // FONT_SIZE)
        wList = wrapp.wrap(text=textToWarp)
  
        for line in wList: 
            row = layout.row(align = True)
            row.alignment = 'EXPAND'
            row.scale_y = 0.6
            row.label(text=line)
    
    def draw(self, context):
        layout = self.layout
        self.draw_tip_text(context, layout)
        layout.prop(self, "export_normal")
        layout.prop(self, "print_result")
        
    def get_or_create_image(self, alpha, float_buffer):
        if self.vat_name in bpy.data.images:
            unique_id = str(uuid.uuid4())
            image_name = f"{self.vat_name}_{unique_id}"
        
        bpy.ops.image.new(name=self.vat_name, width=self.width, height=self.height, alpha=alpha, float=True)
        image = bpy.data.images[self.vat_name]
        image.colorspace_settings.name = "Non-Color"
        image.pixels = float_buffer
        return image
    
    def save_and_remove_image(self, image):
        image.file_format = "OPEN_EXR"
        image.save(filepath=self.filepath)
        bpy.data.images.remove(image)
    
    def get_vertex_tangents(self, mesh):
        mesh.calc_tangents()

        vertex_tangents = {i: [] for i in range(len(mesh.vertices))}

        for loop in mesh.loops:
            vertex_index = loop.vertex_index
            tangent = loop.tangent
            bitangent = loop.bitangent_sign * (loop.normal.cross(tangent))
            
            w = loop.bitangent_sign
            tangent_with_w = (tangent.x, tangent.y, tangent.z, w)
            vertex_tangents[vertex_index].append(tangent_with_w)

            
        averaged_tangents = {}
        for vertex_index, tangents in vertex_tangents.items():
            if tangents:
                avg_tangent = [0.0, 0.0, 0.0, 0.0]
                for t in tangents:
                    avg_tangent[0] += t[0]
                    avg_tangent[1] += t[1]
                    avg_tangent[2] += t[2]
                    avg_tangent[3] += t[3]
                    
            avg_tangent = [x / len(tangents) for x in avg_tangent]
            averaged_tangents[vertex_index] = avg_tangent

        return averaged_tangents
        
    def store_index_in_uv(self, mesh):
        bpy.ops.object.mode_set(mode='EDIT')
        bm = bmesh.from_edit_mesh(mesh)
        uv_layer = None
        if "VertexIndex" in bm.loops.layers.uv:
            uv_layer = bm.loops.layers.uv["VertexIndex"]
        else:
            uv_layer = bm.loops.layers.uv.new("VertexIndex")
            
        #for face in bm.faces:
            #loops = face.loops
            #bmesh.ops.split_edges(bm, edges=face.edges)
            
        for vert in bm.verts:
            for loop in vert.link_loops:
                uv = loop[uv_layer]
                uv.uv = (vert.index, 0)
        
        bmesh.update_edit_mesh(mesh)
        bpy.ops.object.mode_set(mode='OBJECT')
    
    def normalize(self, vector):
        norm = np.linalg.norm(vector)
        if norm == 0:
            return vector
        return vector / norm
    
    def get_normal_offset_in_tangent_space(self, normal, tangent, vector_to_convert):
        normal = self.normalize(normal)
        sign = tangent[3]
        tangent = self.normalize(tangent[:3])
        bitangent = np.cross(normal, tangent) * sign
        
        n_t = np.dot(vector_to_convert, tangent)
        n_b = np.dot(vector_to_convert, bitangent)
        n_n = np.dot(vector_to_convert, normal)
        
        normal_in_tangent_space = self.normalize(np.array([n_t, n_b, n_n]))
        
        return normal_in_tangent_space
    
    def pack_vector2_to_float(self, vector):
        x = vector[0]
        y = vector[1]
        x = np.clip(x, -1, 1) * 0.5 + 0.5
        y = np.clip(y, -1, 1) * 0.5 + 0.5
        
        x_int = np.uint32(x * 0xFFFF)
        y_int = np.uint32(y * 0xFFFF)

        packed = (x_int & 0xFFFF) | ((y_int & 0xFFFF) << 16)
        uint_bytes = struct.pack('I', packed.item())
        float_value = struct.unpack('f', uint_bytes)[0]
        
        return float_value
    
    def pack_vector3_to_float(self, vector):
        x = vector[0]
        y = vector[1]
        z = vector[2]

        x = np.clip(x, -1, 1) * 0.5 + 0.5
        y = np.clip(y, -1, 1) * 0.5 + 0.5
        z = np.clip(z, -1, 1) * 0.5 + 0.5
        
        x_int = np.uint32(x * 0x3FF)
        y_int = np.uint32(y * 0x3FF)
        z_int = np.uint32(z * 0x3FF)

        packed = (x_int & 0x3FF) | ((y_int & 0x3FF) << 10) | ((z_int & 0x3FF) << 20)
        uint_bytes = struct.pack('I', packed.item())
        float_value = struct.unpack('f', uint_bytes)[0]
        return float_value

    def execute(self, context):
        self.report({'INFO'}, "VAT Exported")
        obj = context.view_layer.objects.active
        
        #檢查物件是不是 Mesh
        if obj.type != 'MESH':
            self.report({'ERROR'}, "Active object is not a mesh")
            return {'CANCELLED'}
        
        #
        if not obj.data.uv_layers.active:
            self.report({'ERROR'}, "Object must have UV map to calculate tangents")
        
        #獲取動畫的 frame 範圍
        start_frame = context.scene.frame_start
        end_frame = context.scene.frame_end
        
        #
        self.store_index_in_uv(obj.data)
        
        dg = context.evaluated_depsgraph_get()
        obj = context.object.evaluated_get(dg)
        
        #產生初始 Mesh
        #不能直接透過 obj.data 取得 Mesh ，會取得沒有套用 Shape Keys 的結果
        context.scene.frame_set(0)
        initialMesh = obj.to_mesh(preserve_all_data_layers=True, depsgraph=dg).copy()
        if self.export_normal :
            initialMesh_tangents = self.get_vertex_tangents(initialMesh)
        
        vertex_count = len(initialMesh.vertices)  #Width
        frame_count = end_frame - start_frame + 1 #Height
        self.width = vertex_count
        self.height = frame_count
        
        if self.print_result:
            print(f"Vertex Count {vertex_count} Frame Count(Y){frame_count}")
        
        offset_array = np.zeros((frame_count, vertex_count * RGB_CHANNEL_COUNT), dtype=np.float32) 

        #逐 frame 遍歷 Mesh 的頂點
        for frame in range(start_frame, end_frame + 1):
            context.scene.frame_set(frame)
            deformed_mesh = obj.to_mesh(preserve_all_data_layers=True, depsgraph=dg)
        
            if self.print_result:
                print(f"Frame {frame}:")
            for i, v in enumerate(deformed_mesh.vertices):
                inital_vertex = initialMesh.vertices[i]
                posOffset = v.co - inital_vertex.co
                
                #
                packed_normaloffset = 0
                if self.export_normal :
                    #tanget_space_normaloffset = self.get_normal_offset_in_tangent_space(inital_vertex.normal, initialMesh_tangents[i], v.normal)
                    #packed_normaloffset = self.pack_vector2_to_float(tanget_space_normaloffset)
                    packed_normaloffset = self.pack_vector3_to_float(v.normal)
                
                offset_array[frame - start_frame, i * RGB_CHANNEL_COUNT : i * RGB_CHANNEL_COUNT + RGB_CHANNEL_COUNT] = [posOffset.x, posOffset.y, posOffset.z, packed_normaloffset]
                if self.print_result:
                    print(f"Vertex {i} Position Delta {posOffset} Tangent Normal {v.normal} {packed_normaloffset}")
           
        #儲存並移除暫存的 VAT
        vatImage = self.get_or_create_image(self.export_normal, offset_array.flatten())
        self.save_and_remove_image(vatImage)
        
        bpy.data.meshes.remove(initialMesh)
        
        context.scene.frame_set(0)
        return {'FINISHED'}

def menu_func(self, context):
    self.layout.operator(VertexAnimationTextureOperator.bl_idname, text="Vertex Animation Texture Exporter")

def register():
    bpy.utils.register_class(VertexAnimationTextureOperator)
    bpy.types.TOPBAR_MT_file_export.append(menu_func)

def unregister():
    bpy.utils.unregister_class(VertexAnimationTextureOperator)
    bpy.types.TOPBAR_MT_file_export.remove(menu_func)

if __name__ == "__main__":
    register()


