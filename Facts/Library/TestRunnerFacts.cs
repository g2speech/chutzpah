﻿using System;
using System.Collections.Generic;
using System.IO;
using Chutzpah.Facts.Mocks;
using Chutzpah.Models;
using Chutzpah.Wrappers;
using Moq;
using Xunit;

namespace Chutzpah.Facts
{
    public class TestRunnerFacts
    {
        private class TestableTestRunner : TestRunner
        {
            public Mock<IProcessWrapper> MoqProcessWrapper { get; set; }
            public Mock<ITestResultsBuilder> MoqTestResultsBuilder { get; set; }
            public Mock<IFileProbe> MoqFileProbe { get; set; }
            public Mock<ITestContextBuilder> MoqTestContextBuilder { get; set; }


            private TestableTestRunner(Mock<IProcessWrapper> moqProcessWrapper,
                                       Mock<ITestResultsBuilder> moqTestResultsBuilder,
                                       Mock<IFileProbe> moqFileProbe,
                                       Mock<ITestContextBuilder> moqHtmlTestFileCreator)
                : base(moqProcessWrapper.Object, moqTestResultsBuilder.Object, moqFileProbe.Object, moqHtmlTestFileCreator.Object)
            {
                MoqProcessWrapper = moqProcessWrapper;
                MoqTestResultsBuilder = moqTestResultsBuilder;
                MoqFileProbe = moqFileProbe;
                MoqTestContextBuilder = moqHtmlTestFileCreator;
            }

            public static TestableTestRunner Create()
            {
                var runner = new TestableTestRunner(
                    new Mock<IProcessWrapper>(),
                    new Mock<ITestResultsBuilder>(),
                    new Mock<IFileProbe>(),
                    new Mock<ITestContextBuilder>());

                runner.MoqFileProbe.Setup(x => x.FindPath(It.IsAny<string>())).Returns("");

                return runner;
            }
        }

        public class GetTestContext
        {
            [Fact]
            public void Will_get_test_context()
            {
                var runner = TestableTestRunner.Create();
                var context = new TestContext();
                runner.MoqTestContextBuilder.Setup(x => x.BuildContext("a.js")).Returns(context);
                
                var res = runner.GetTestContext("a.js");

                Assert.Equal(context, res);
            }
        }

        public class RunTests
        {
            [Fact]
            public void Will_throw_if_test_files_collection_is_null()
            {
                TestableTestRunner runner = TestableTestRunner.Create();
                var ex = Record.Exception(() => runner.RunTests((IEnumerable<string>) null)) as ArgumentNullException;

                Assert.NotNull(ex);
            }

            [Fact]
            public void Will_throw_if_headless_browser_does_not_exist()
            {
                TestableTestRunner runner = TestableTestRunner.Create();
                runner.MoqFileProbe.Setup(x => x.FindPath(TestRunner.HeadlessBrowserName)).Returns((string) null);

                var ex = Record.Exception(() => runner.RunTests("someFile")) as FileNotFoundException;

                Assert.NotNull(ex);
            }

            [Fact]
            public void Will_throw_if_test_runner_js_does_not_exist()
            {
                TestableTestRunner runner = TestableTestRunner.Create();
                runner.MoqFileProbe.Setup(x => x.FindPath(TestRunner.TestRunnerJsName)).Returns((string) null);

                var ex = Record.Exception(() => runner.RunTests("someFile")) as FileNotFoundException;

                Assert.NotNull(ex);
            }

            [Fact]
            public void Will_run_test_file_and_return_test_results_model()
            {
                TestableTestRunner runner = TestableTestRunner.Create();
                BrowserTestFileResult fileResult = null;
                var testResults = new List<TestResult> {new TestResult()};
                var context = new TestContext { TestHarnessPath = @"D:\harnessPath.html" };
                runner.MoqTestContextBuilder.Setup(x => x.BuildContext(@"path\tests.html")).Returns(context);
                runner.MoqFileProbe.Setup(x => x.FindPath(TestRunner.TestRunnerJsName)).Returns("jsPath");
                runner.MoqFileProbe.Setup(x => x.FindPath(TestRunner.HeadlessBrowserName)).Returns("browserPath");
                runner.MoqFileProbe.Setup(x => x.FindPath(@"path\tests.html")).Returns(@"D:\path\tests.html");
                string args = "\"jsPath\"" + @" ""file:///D:/harnessPath.html""";
                runner.MoqProcessWrapper.Setup(x => x.RunExecutableAndCaptureOutput("browserPath", args)).Returns("json");
                runner.MoqTestResultsBuilder.Setup(x => x.Build(It.IsAny<BrowserTestFileResult>())).Callback<BrowserTestFileResult>(x => fileResult = x).Returns(testResults);

                TestResultsSummary res = runner.RunTests(@"path\tests.html");

                Assert.Equal(1, res.TotalCount);
                Assert.Equal(context, fileResult.TestContext);
                Assert.Equal("json", fileResult.BrowserOutput);
            }

            [Fact]
            public void Will_run_multiple_test_files_and_return_results()
            {
                TestableTestRunner runner = TestableTestRunner.Create();
                var fileResults = new List<BrowserTestFileResult>();
                var testResults = new List<TestResult> {new TestResult()};
                var context1 = new TestContext { TestHarnessPath = @"D:\harnessPath1.html" };
                var context2 = new TestContext { TestHarnessPath = @"D:\harnessPath2.htm" };
                runner.MoqTestContextBuilder.Setup(x => x.BuildContext(@"path\tests1.html")).Returns(context1);
                runner.MoqTestContextBuilder.Setup(x => x.BuildContext(@"path\tests2.htm")).Returns(context2);
                runner.MoqFileProbe.Setup(x => x.FindPath(@"path\tests1.html")).Returns(@"D:\path\tests1.html");
                runner.MoqFileProbe.Setup(x => x.FindPath(@"path\tests2.htm")).Returns(@"D:\path\tests2.htm");
                runner.MoqFileProbe.Setup(x => x.FindPath(TestRunner.TestRunnerJsName)).Returns("jsPath");
                string args1 = "\"jsPath\"" + @" ""file:///D:/harnessPath1.html""";
                string args2 = "\"jsPath\"" + @" ""file:///D:/harnessPath2.htm""";
                runner.MoqProcessWrapper.Setup(x => x.RunExecutableAndCaptureOutput(It.IsAny<string>(), args1)).Returns("json1");
                runner.MoqProcessWrapper.Setup(x => x.RunExecutableAndCaptureOutput(It.IsAny<string>(), args2)).Returns("json2");
                runner.MoqTestResultsBuilder.Setup(x => x.Build(It.IsAny<BrowserTestFileResult>())).Callback<BrowserTestFileResult>(fileResults.Add).Returns(testResults);

                TestResultsSummary res = runner.RunTests(new List<string> {@"path\tests1.html", @"path\tests2.htm"});

                Assert.Equal(2, res.TotalCount);
                Assert.Equal(context1, fileResults[0].TestContext);
                Assert.Equal("json1", fileResults[0].BrowserOutput);
                Assert.Equal(context2, fileResults[1].TestContext);
                Assert.Equal("json2", fileResults[1].BrowserOutput);
            }

            [Fact]
            public void Will_call_test_suite_started()
            {
                TestableTestRunner runner = TestableTestRunner.Create();
                var testCallback = new MockTestMethodRunnerCallback();
                runner.MoqFileProbe.Setup(x => x.FindPath(@"path\tests.html")).Returns(@"D:\path\tests.html");
                runner.MoqTestContextBuilder.Setup(x => x.BuildContext(@"path\tests.html")).Returns(new TestContext { TestHarnessPath = @"D:\harnessPath.html" });
                string args = TestRunner.TestRunnerJsName + @" file:///D:/harnessPath.html";

                TestResultsSummary res = runner.RunTests(@"path\tests.html", testCallback.Object);

                testCallback.Verify(x => x.TestSuiteStarted());
            }

            [Fact]
            public void Will_call_test_suite_finished_with_final_result()
            {
                TestableTestRunner runner = TestableTestRunner.Create();
                var testCallback = new MockTestMethodRunnerCallback();
                var testResults = new List<TestResult> {new TestResult()};
                runner.MoqTestContextBuilder.Setup(x => x.BuildContext(@"path\tests.html")).Returns(new TestContext { TestHarnessPath = @"D:\harnessPath.html" });
                runner.MoqFileProbe.Setup(x => x.FindPath(@"path\tests.html")).Returns(@"D:\path\tests.html");
                runner.MoqFileProbe.Setup(x => x.FindPath(TestRunner.TestRunnerJsName)).Returns("jsPath");
                string args = "jsPath" + @" file:///D:/harnessPath.html";
                runner.MoqProcessWrapper.Setup(x => x.RunExecutableAndCaptureOutput(It.IsAny<string>(), args)).Returns("json");
                runner.MoqTestResultsBuilder.Setup(x => x.Build(It.IsAny<BrowserTestFileResult>())).Returns(testResults);

                TestResultsSummary res = runner.RunTests(@"path\tests.html", testCallback.Object);

                testCallback.Verify(x => x.TestSuiteFinished(It.IsAny<TestResultsSummary>()));
            }

            [Fact]
            public void Will_call_file_started_on_callback_and_stop_test_if_return_is_false()
            {
                TestableTestRunner runner = TestableTestRunner.Create();
                var testCallback = new MockTestMethodRunnerCallback();
                runner.MoqTestContextBuilder.Setup(x => x.BuildContext(@"path\tests.html")).Returns(new TestContext { TestHarnessPath = @"D:\harnessPath.html", InputTestFile = @"D:\path\tests.html" });
                testCallback.Setup(x => x.FileStart(@"D:\path\tests.html")).Returns(false).Verifiable();
                string args = TestRunner.TestRunnerJsName + @" file:///D:/harnessPath.html";

                TestResultsSummary res = runner.RunTests(@"path\tests.html", testCallback.Object);

                testCallback.Verify();
                runner.MoqProcessWrapper.Verify(x => x.RunExecutableAndCaptureOutput(TestRunner.HeadlessBrowserName, args), Times.Never());
            }

            [Fact]
            public void Will_call_file_finished_on_callback_and_stop_test_if_return_is_false()
            {
                TestableTestRunner runner = TestableTestRunner.Create();
                var testResults = new List<TestResult> {new TestResult()};
                var testCallback = new MockTestMethodRunnerCallback();
                runner.MoqTestContextBuilder.Setup(x => x.BuildContext(@"path\tests.html")).Returns(new TestContext { TestHarnessPath = @"D:\harnessPath.html", InputTestFile = @"D:\path\tests.html" });
                testCallback.Setup(x => x.FileFinished(@"D:\path\tests.html", It.Is<TestResultsSummary>(t => t.TotalCount == 1))).Returns(false).Verifiable();
                string args = TestRunner.TestRunnerJsName + @" file:///D:/harnessPath.html";
                runner.MoqProcessWrapper.Setup(x => x.RunExecutableAndCaptureOutput(TestRunner.HeadlessBrowserName, args)).Returns("json");
                runner.MoqTestResultsBuilder.Setup(x => x.Build(It.IsAny<BrowserTestFileResult>())).Returns(testResults);

                TestResultsSummary res = runner.RunTests(new[] {@"path\tests.html", @"path\tests.html"}, testCallback.Object);

                testCallback.Verify();
                Assert.Equal(1, res.TotalCount);
            }

            [Fact]
            public void Will_call_test_finished_on_callback_for_each_test()
            {
                TestableTestRunner runner = TestableTestRunner.Create();
                var testResults = new List<TestResult> {new TestResult(), new TestResult()};
                var testCallback = new MockTestMethodRunnerCallback();
                runner.MoqTestContextBuilder.Setup(x => x.BuildContext(@"path\tests.html")).Returns(new TestContext { TestHarnessPath = @"D:\harnessPath.html" });
                runner.MoqFileProbe.Setup(x => x.FindPath(@"path\tests.html")).Returns(@"D:\path\tests.html");
                string args = TestRunner.TestRunnerJsName + @" file:///D:/harnessPath.html";
                runner.MoqProcessWrapper.Setup(x => x.RunExecutableAndCaptureOutput(TestRunner.HeadlessBrowserName, args)).Returns("json");
                runner.MoqTestResultsBuilder.Setup(x => x.Build(It.IsAny<BrowserTestFileResult>())).Returns(testResults);

                TestResultsSummary res = runner.RunTests(new[] {@"path\tests.html", @"path\tests.html"}, testCallback.Object);

                testCallback.Verify(x => x.TestFinished(testResults[0]));
                testCallback.Verify(x => x.TestFinished(testResults[1]));
            }

            [Fact]
            public void Will_call_exception_thrown_on_callback_and_move_to_next_test_file()
            {
                TestableTestRunner runner = TestableTestRunner.Create();
                var testResults = new List<TestResult> {new TestResult()};
                var testCallback = new MockTestMethodRunnerCallback();
                var exception = new Exception();
                runner.MoqTestContextBuilder.Setup(x => x.BuildContext(@"path\tests1.html")).Throws(exception);
                runner.MoqTestContextBuilder.Setup(x => x.BuildContext(@"path\tests2.html")).Returns(new TestContext { TestHarnessPath = @"D:\harnessPath.html" });
                runner.MoqFileProbe.Setup(x => x.FindPath(@"path\tests1.html")).Throws(exception);
                runner.MoqFileProbe.Setup(x => x.FindPath(@"path\tests2.html")).Returns(@"D:\path\tests2.html");
                string args = TestRunner.TestRunnerJsName + @" file:///D:/harnessPath.html";
                runner.MoqProcessWrapper.Setup(x => x.RunExecutableAndCaptureOutput(TestRunner.HeadlessBrowserName, args)).Returns("json");
                runner.MoqTestResultsBuilder.Setup(x => x.Build(It.IsAny<BrowserTestFileResult>())).Returns(testResults);

                TestResultsSummary res = runner.RunTests(new[] {@"path\tests1.html", @"path\tests2.html"}, testCallback.Object);

                testCallback.Verify(x => x.ExceptionThrown(exception, @"path\tests1.html"));
                Assert.Equal(1, res.TotalCount);
            }
        }
    }
}