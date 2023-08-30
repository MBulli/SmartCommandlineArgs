#include "pch.h"
#include <iostream>
#include <cstdlib>

int main(int argc, char* argv[]) {
    // Display command line arguments
    std::cout << "Command Line Arguments:" << std::endl;
    for (int i = 0; i < argc; ++i) {
        std::cout << argv[i] << std::endl;
    }

    // Display environment variables
    std::cout << "\nEnvironment Variables:" << std::endl;
    for (char** env = environ; *env; ++env) {
        std::cout << *env << std::endl;
    }

    return 0;
}
