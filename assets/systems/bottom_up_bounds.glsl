#[compute]
#version 450

layout(local_size_x = 256) in;

struct AABB {
    vec4 minPosition;
    vec4 maxPosition;
};

struct SortEntry{
    uint codeLo;
    uint codeHi;
    uint index;
};

struct Node{
    uint left;
    uint right;
    uint parent;
    uint pad;
};

layout(set=0, binding=0, std430) readonly buffer EntryBuffer{
    SortEntry entries[];
};

layout(set=0, binding=1, std430) buffer NodesBuffer{
    Node nodes[];
};

layout(set=0, binding=2, std430) readonly buffer PrimBounds{
    AABB prim[];
};

layout(set=0, binding=3, std430) coherent buffer Bounds{
    AABB bounds[];
};

layout(set=0, binding=4, std430) coherent buffer Counters{
    uint counters[];
};

layout(push_constant) uniform Params{
    uint count;
    uint p0;
    uint p1;
    uint p2;
} params;

void main() {
    int i = int(gl_GlobalInvocationID.x);
    int n = int(params.count);
    if (i >= n) return;

    uint node = uint(n - 1 + i);
    bounds[node] = prim[entries[i].index];

    while (true) {
        uint p = nodes[node].parent;
        if (p == 0xFFFFFFFFu) return;

        memoryBarrierBuffer();
        uint old = atomicAdd(counters[p], 1u);
        if (old == 0u) return; // sibling not done, it'll finish this one
        memoryBarrierBuffer();

        uint l = nodes[p].left, r = nodes[p].right;
        bounds[p].minPosition = min(bounds[l].minPosition, bounds[r].minPosition);
        bounds[p].maxPosition = max(bounds[l].maxPosition, bounds[r].maxPosition);
        node = p;
    }
}