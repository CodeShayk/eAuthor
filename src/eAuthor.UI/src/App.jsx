import './App.css'
import {Footer} from "./components/Footer.jsx";
import {Header} from "./components/Header.jsx";
import {ProjectGrid} from "./components/ProjectGrid.jsx";
import {data} from "./data/data.js";

function App() {




    return (
        <>
            <div className="container">
                <Header developer = {data.developer}></Header>
                <ProjectGrid projects = {data.projects} />
                <Footer copyright={data.copyright}></Footer>
            </div>
        </>

    );

}
export default App

