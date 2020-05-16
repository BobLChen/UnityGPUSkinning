# UnityGPUSkinning
Simple and fast gpu skinning in unity.

# 依赖
无任何依赖，OpenGL ES2.0 都可以使用，只要把Shader中的骨骼数量降低即可(Constant Vector不要超过256即可)。

# 实现原理
对偶四元数替换矩阵实现

# 功能兼容性
- BlendTree不支持
- Animator支持
