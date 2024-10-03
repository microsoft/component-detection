import os
import codecs
from setuptools import find_packages, setup

def read(fname):
    file_path = os.path.join(os.path.dirname(__file__), fname)
    return codecs.open(file_path, encoding='utf-8').read()

setup(
    name='pytest_pyresultreport', 
    version='0.1.0',
    author='CG',
    package_dir = {"":"src"}, 
    packages = find_packages(where="src"),  
    maintainer='CG',
    description='plugin to modify pytest reports',
    python_requires='>=3.7',
    install_requires=['pytest>=3.7'],
    classifiers=[
        'Framework :: Pytest',
        'Intended Audience :: Developers',
        'Topic :: Software Development :: Testing',
        'Programming Language :: Python :: 3.7',
        'Programming Language :: Python :: 3.8',
        'Programming Language :: Python :: 3.9',
        'Programming Language :: Python :: 3.10',
        'Programming Language :: Python :: Implementation :: CPython',
        'Programming Language :: Python :: Implementation :: PyPy',
        'Operating System :: OS Independent',
    ],
    entry_points={
        'pytest11': [ 
            'py_result_report = pytest_pyresultreport.plugin',
        ],
    },
)