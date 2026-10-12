#[compute]
#version 450

const int BIAS = 1 << 20;

layout(local_size_x=64)in;

struct AABB{
    vec4 minPosition;
    vec4 maxPosition;
};

struct SortEntry{
    uint codeLo;
    uint codeHi;
    uint index;
};

layout(std430, binding=0) readonly buffer AabbBuffer {
    AABB aabbs[];
};

layout(std430, binding=1) writeonly buffer EntryBuffer{
    SortEntry entries[];
};

layout(std140, binding=2) uniform Params{
    float cell;
    uint paddedCount;
    uint count;
};

// Spread 10 bits so there are 2 zero bits between each (bits 0..9 -> 0,3,..,27)
uint expand10(uint v) {
    v &= 0x3FFu;
    v = (v | (v << 16)) & 0x030000FFu;
    v = (v | (v <<  8)) & 0x0300F00Fu;
    v = (v | (v <<  4)) & 0x030C30C3u;
    v = (v | (v <<  2)) & 0x09249249u;
    return v;
}

// 21-bit input -> 63-bit spread result as uvec2(lo, hi)
// bit k of x lands at bit 3k:
//   k = 0..10  -> bits 0..30   (lo word)
//   k = 11..20 -> bits 33..60  (hi word, offset by 1)
uvec2 part1By2(uint x){
    x &= 0x1FFFFFu;
    uint lo = expand10(x) | (((x >> 10) & 1u) << 30);
    uint hi = expand10(x >> 11) << 1;
    return uvec2(lo, hi);
}

uvec2 shl1(uvec2 v){
    return uvec2(v.x << 1, (v.y << 1) | (v.x >> 31));
}

uvec2 shl2(uvec2 v){
    return uvec2(v.x << 2, (v.y << 2) | (v.x >> 30));
}

uvec2 mortonEncode(uvec3 c){
    uvec2 x = part1By2(c.x);
    uvec2 y = shl1(part1By2(c.y));
    uvec2 z = shl2(part1By2(c.z));
    return x | y | z;
}

ivec3 floorPosition(vec3 p){
    return ivec3(floor(p / cell));
}

uvec3 toUnsigned(ivec3 v){
    return uvec3(v + ivec3(BIAS));
}

void main() {
    uint i = gl_GlobalInvocationID.x;
    if (i >= paddedCount) return;
    if (i >= count){
        entries[i].codeLo = 0xFFFFFFFFu;
        entries[i].codeHi = 0xFFFFFFFFu;
        entries[i].index  = 0xFFFFFFFFu;
        return;
    }

    vec3 center = (aabbs[i].minPosition.xyz + aabbs[i].maxPosition.xyz) * 0.5;

    uvec2 code = mortonEncode(toUnsigned(floorPosition(center)));
    entries[i].codeLo = code.x;
    entries[i].codeHi = code.y;
    entries[i].index  = i;
}