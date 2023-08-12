from conans import ConanFile

class MyAwesome(ConanFile):
    name = "MyAwesomeConanProject"
    version = "1.2.5"

    def requirements(self):
        self.requires("boost/1.82.0")

    def build_requirements(self):
        self.tool_requires("gtest/1.8.1")
