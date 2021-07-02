#==============================
# Requirements
#==============================

cmake_minimum_required(VERSION 3.10)

#==============================
# Project Settings
#==============================

if (NOT PROJECT_DECLARED)
    project("Gaia Serial IO Service" LANGUAGES CXX VERSION 0.9)
    set(PROJECT_DECLARED)
endif()

#==============================
# Project Modules
#==============================

add_subdirectory("GaiaSerialIOClient")
