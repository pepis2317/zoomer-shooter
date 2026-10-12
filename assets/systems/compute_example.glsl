#[compute]
#version 450

// We weant to pass an input array to our shader, multiply each element by 2 and get the results
// Invocations in the x, y, z dimension
// Think of invocations as workers, how many copies of main() in each work group
// If you look at your CPU script, you defined 5 work groups, which fits into the array of 10 floats you want to process
layout(local_size_x = 2, local_size_y = 1, local_size_z = 1) in;

// A binding to the buffer we create in our CPU script
layout(set = 0, binding = 0, std430) restrict buffer MyDataBuffer {
    float data[];
} my_data_buffer;

void main() {
    // Each worker uses its global id as index into the array and doubles that one element
    my_data_buffer.data[gl_GlobalInvocationID.x] *= 2.0;
}