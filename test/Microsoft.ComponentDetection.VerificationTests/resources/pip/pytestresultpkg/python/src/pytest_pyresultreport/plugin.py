import pytest
from pytest_jsonreport import plugin as jsonreportplugin
from collections import defaultdict 

test_results = defaultdict(list)

class TestResults: 
    def __init__(self): 
        self.skipped_tests = []
        self.xfailed_tests = [] 
        self.xpassed_tests = [] 
        self.failed_tests = [] 
        self.error_tests = [] 
        self.test_duration = 0

def pytest_json_modifyreport(json_report):
    global test_results
    curr_test_results = TestResults()
    tests = json_report["tests"]
    test_duration = json_report["duration"]

    for test in tests: 
        node_id = test["nodeid"] 
        if test["outcome"] == "passed": 
            continue 
        elif test["outcome"] == "xpassed": 
            test_info = defaultdict()
            test_info["name"] = node_id
            test_info["error_message"] = "No Error Message"
            curr_test_results.xpassed_tests.append(test_info)
        else: 
            setup_outcome = test["setup"]["outcome"] 
            call_outcome = test["call"]["outcome"] if "call" in test else "" 
            teardown_outcome = test["teardown"]["outcome"] if "teardown" in test else "" 

            if setup_outcome != "passed": 
                err_msg = test["setup"]["longrepr"]
            elif call_outcome and call_outcome != "passed":
                err_msg = test["call"]["longrepr"]
            else: 
                err_msg = test["teardown"]["longrepr"]

            test_info = defaultdict()
            test_info["name"] = node_id
            test_info["error_message"] = err_msg 

            if test["outcome"] == "skipped": 
                curr_test_results.skipped_tests.append(test_info)
            elif test["outcome"] == "xfailed":
                curr_test_results.xfailed_tests.append(test_info)  
            elif test["outcome"] == "failed": 
                curr_test_results.failed_tests.append(test_info)
            elif test["outcome"] == "error": 
                curr_test_results.error_tests.append(test_info)

    curr_test_results.test_duration = test_duration
    test_results = curr_test_results

def pytest_configure(config): # registering pytest-json-report plugin. 
    json_plugin = jsonreportplugin.JSONReport(config)
    config.pluginmanager.register(json_plugin)
    config._json_report = json_plugin