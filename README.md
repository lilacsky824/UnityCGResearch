Research on computer graphics using the Unity engine. Detailed results can be found in each branch. 
Using Unity version 6000.0.32f1 with Universal Render Pipeline.

# Parallax Occlusion Mapping with Depth Offset
A Lit shader that is support parallax ccclusion mapping with per pixel depth offset. Including support shadow caster or use self shadowing.
Some derivation processes can be found in the [blog post (in Traditional Chinese only)](https://lilacsky824.blogspot.com/2025/01/unity-shader-parallax-occlusion-mapping.html).
<video src="https://github.com/user-attachments/assets/5568383d-3ebd-4797-89f0-ba9e761c911d"></video>
![ParallaxOcclusionMapping](https://github.com/user-attachments/assets/3c3d0200-5cd8-4690-8bc6-f423eee922d6)

# Jump Flooding Algorithm
Generating Voronoi diagrams and distance fields using Compute Shaders.

![JFA_001](https://github.com/lilacsky824/UnityCGResearch/assets/75205949/aa4eb4dd-4f2b-4b2c-b28f-7dff70b4dc81)
![JFA_002](https://github.com/lilacsky824/UnityCGResearch/assets/75205949/1017db6b-3ce5-4aec-9744-ce4cf83f96b2)

# Toon Map
Ramp Map and Gradient Map generators, using Compute Shader.
![Toon Map](https://github.com/lilacsky824/UnityCGResearch/assets/75205949/20ea9302-5733-4c5d-8ef5-0ea007fb5830)

# Vertex Animation Texture(VAT)
## Spine
Bake Spine skeleton animation into VAT and use the Particle System for random playback, creating the effect of a crowd running.
<video src="https://github.com/user-attachments/assets/0b5d9a97-88d3-41db-9619-a9d9a58dd20e"></video>

## Blender(draft)
Bake vertex animation in Blender into VAT and bake object space normals. Currently, Split Per-Vertex Normals is not supported.
RGB channels store the vertex positions, while the alpha channel stores the normals using quantization.
<video src="https://github.com/user-attachments/assets/f3ca7938-26ab-47f5-a93f-7fca45182618"></video>
