#include <iostream>
#include <vector>

namespace demo {
class Point {
public:
    int x;
    int y;

    int sum() const {
        return x + y;
    }
};

int add(int a, int b) {
    int result = a + b;
    return result;
}
}

int main() {
    demo::Point p{10, 20};
    std::vector<int> xs{1, 2, 3};

    if (p.sum() > 0) {
        for (int i = 0; i < 3; ++i) {
            xs[i] = xs[i] + 1;
        }
    }

    return 0;
}
