#[compute]
#version 450

layout(local_size_x = 256) in;

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

layout(push_constant) uniform Params{
    uint count;
    uint p0;
    uint p1;
    uint p2;
} params;

int n;
// Returns length of the longest common prefix in bits, we use this as the 'closeness score'
// Higher the return value = keys are more similar = entries are closer in the tree
int delta(int i, int j){
    // Prevent out of range results. j should always be >= 0
    if( j < 0 || j >= n ) return -1;
    SortEntry a = entries[i];
    SortEntry b = entries[j];

    // The upper 32 bits are not the same
    if(a.codeHi != b.codeHi){
        // findMSB stands for 'find most significant bit'. it returns the position of the highest 1
        // every bit above findMSB(a.codeHi ^ b.codeHi) matched
        // so 31 (like the range 0...31) - msb gives us the closeness score
        return 31 - findMSB(a.codeHi ^ b.codeHi);
    }
    // The lower 32 bits are not the same
    if(a.codeLo != b.codeLo){
        // same applies here, but for the lower end of the code
        return 32 + 31 - findMSB(a.codeLo ^ b.codeLo);
    }
    // This is when the full 64 bit morton codes are identical.
    // Tie break using array positions i and j as if they were appended to the tree
    return 64 + 31 - findMSB(uint(i) ^ uint(j));
}

void main(){
    int i = int(gl_GlobalInvocationID.x);
    n = int(params.count);
    if(i >= n-1) return;

    // direction of the range
    // if the index after this is 'closer' than the index before, 1, else -1
    int direction = delta(i, i + 1) - delta(i, i - 1) > 0 ? 1 : -1;
    // look at the score of the prev/next index
    int dmin = delta(i, i - direction);

    // upper bound on range length
    // start with a guess of 2
    int lmax = 2;
    // probe to the right until the score between this index and i + lmax * direction is 'closer' than dmin
    while(delta(i, i + lmax * direction) > dmin){
        lmax *= 2;
    }

    // binary search for the other end
    // l is the distance from i to the last entry that shares more than dmin bits
    int l = 0;
    for(int t = lmax/2; t >= 1; t /= 2){
        if(delta(i, i + (l + t) * direction) > dmin){
            l += t;
        }
    }
    // the result i j, an index of the other end of node
    int j = i + l * direction;

    //binary search for the split position
    int dnode = delta(i, j);
    // s is going to be the distance from i to the last entry that still shares more than dnode
    int s = 0;
    // t is the step size, starts at l (range length) and shrinks each iteration
    int t = l;
    do{
        t = (t + 1) >> 1;
        if(delta(i, i + (s + t) * direction) > dnode){
            s += t;
        }
    }while(t > 1);
    // i + s * direction is the index of the last entry in i's child
    // gamma noramlizes this so that its always the left element of the split pair(gamma & gamma + 1)
    // idk man, beats me
    int gamma = i + s * direction + min(direction, 0);

    int leafBase = n - 1;
    uint left  = (min(i, j) == gamma) ? uint(leafBase + gamma)     : uint(gamma);
    uint right = (max(i, j) == gamma + 1) ? uint(leafBase + gamma + 1) : uint(gamma + 1);

    nodes[i].left = left;
    nodes[i].right = right;
    nodes[left].parent = uint(i);
    nodes[right].parent = uint(i);
    if (i == 0) nodes[0].parent = 0xFFFFFFFFu; // root


}